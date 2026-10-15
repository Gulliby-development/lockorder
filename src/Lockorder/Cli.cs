using System.Security;
using System.Text;
using Lockorder.Core;

namespace Lockorder;

public static class Cli
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdin);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (LockorderException exception)
        {
            WriteError(stderr, exception.Message);
            return 2;
        }

        if (options.ShowHelp)
        {
            stdout.Write(Report.Usage);
            return 0;
        }

        var display = options.Trace == "-" ? "<stdin>" : options.Trace.Replace('\\', '/');
        IReadOnlyList<TraceEvent> events;
        try
        {
            string text = options.Trace == "-"
                ? stdin.ReadToEnd()
                : ReadFile(options.Trace, display);

            events = Trace.Parse(display, text);
        }
        catch (InputReadException exception)
        {
            WriteError(stderr, exception.Message);
            return 2;
        }
        catch (IOException)
        {
            WriteError(stderr, $"{display}: cannot read the file");
            return 2;
        }
        catch (LockorderException exception)
        {
            WriteError(stderr, exception.Message);
            return 2;
        }

        ReplayResult replay = Replay.Run(events, options.IgnoredLocks);
        var graph = new LockGraph(replay);

        switch (options.Command)
        {
            case "check":
                stdout.Write(Report.Check(display, replay, graph));
                return replay.Problems.Count > 0 || Findings.Create(replay, graph).Any(finding => finding.Code == FindingCode.LK201)
                    ? 1
                    : 0;

            case "edges":
                stdout.Write(Report.Edges(replay));
                return 0;

            case "order":
                if (graph.HasCycle)
                {
                    WriteError(stderr, Report.NoOrderError(display, graph));
                    return 1;
                }

                stdout.Write(Report.Order(graph));
                return 0;

            case "threads":
                stdout.Write(Report.Threads(replay));
                return 0;

            default:
                throw new InvalidOperationException("Options.Parse returned an unsupported command.");
        }
    }

    private static string ReadFile(string path, string display)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or SecurityException
            or ArgumentException
            or NotSupportedException)
        {
            throw new InputReadException($"{display}: cannot read the file");
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new InputReadException($"{display}: input is not valid UTF-8");
        }
    }

    private static void WriteError(TextWriter stderr, string message)
    {
        stderr.Write($"lockorder: {message}\n");
    }

    private sealed class InputReadException : Exception
    {
        public InputReadException(string message)
            : base(message)
        {
        }
    }
}
