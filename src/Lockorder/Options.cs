namespace Lockorder;

public sealed record Options(
    string Command,
    string Trace,
    IReadOnlySet<string> IgnoredLocks,
    bool ShowHelp)
{
    public static Options Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 1 && args[0] == "--help")
        {
            return new Options(string.Empty, string.Empty, new HashSet<string>(StringComparer.Ordinal), true);
        }

        if (args.Length == 0)
        {
            throw new Lockorder.Core.LockorderException("missing command (check, edges, order or threads)");
        }

        var command = args[0];
        if (command is not ("check" or "edges" or "order" or "threads"))
        {
            throw new Lockorder.Core.LockorderException($"unknown command {Lockorder.Core.Names.Show(command)}");
        }

        var ignoredLockValues = new List<string>();
        var ignoredLocks = new HashSet<string>(StringComparer.Ordinal);
        var traceArguments = new List<string>();

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument == "--ignore")
            {
                if (index + 1 >= args.Length)
                {
                    throw new Lockorder.Core.LockorderException("--ignore needs a value");
                }

                ignoredLockValues.Add(args[++index]);
            }
            else if (argument.StartsWith("--ignore=", StringComparison.Ordinal))
            {
                ignoredLockValues.Add(argument["--ignore=".Length..]);
            }
            else if (argument.StartsWith("-", StringComparison.Ordinal) && argument != "-")
            {
                throw new Lockorder.Core.LockorderException($"unknown option {Lockorder.Core.Names.Show(argument)}");
            }
            else
            {
                traceArguments.Add(argument);
            }
        }

        foreach (var ignoredLock in ignoredLockValues)
        {
            if (!Lockorder.Core.Names.IsValidName(ignoredLock))
            {
                throw new Lockorder.Core.LockorderException(
                    $"invalid --ignore {Lockorder.Core.Names.Show(ignoredLock)} (expected a lock name)");
            }

            ignoredLocks.Add(ignoredLock);
        }

        if (traceArguments.Count == 0)
        {
            throw new Lockorder.Core.LockorderException("missing TRACE");
        }

        if (traceArguments.Count > 1)
        {
            throw new Lockorder.Core.LockorderException("too many arguments");
        }

        return new Options(command, traceArguments[0], ignoredLocks, false);
    }
}
