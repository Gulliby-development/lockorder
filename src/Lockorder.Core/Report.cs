namespace Lockorder.Core;

public static class Report
{
    public const string Usage =
        "usage: lockorder check [--ignore LOCK]... TRACE\n" +
        "       lockorder edges [--ignore LOCK]... TRACE\n" +
        "       lockorder order [--ignore LOCK]... TRACE\n" +
        "       lockorder threads [--ignore LOCK]... TRACE\n";

    public static string Check(string display, ReplayResult replay, LockGraph graph)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(graph);

        IReadOnlyList<Finding> findings = Findings.Create(replay, graph);
        if (findings.Count == 0)
        {
            var locks = replay.Locks.Count;
            var threads = replay.Threads.Count;
            return $"{display}: ok, {locks} {(locks == 1 ? "lock" : "locks")}, " +
                $"{threads} {(threads == 1 ? "thread" : "threads")}\n";
        }

        return string.Concat(findings.Select(finding => FormatFinding(display, finding)));
    }

    public static string Edges(ReplayResult replay)
    {
        ArgumentNullException.ThrowIfNull(replay);

        var rows = replay.Edges.Select(edge => new[]
        {
            edge.From,
            edge.To,
            edge.Threads.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            edge.FirstLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

        return Table.Render(
            ["FROM", "TO", "THREADS", "LINE"],
            rows,
            [2, 3]);
    }

    public static string Threads(ReplayResult replay)
    {
        ArgumentNullException.ThrowIfNull(replay);

        var rows = replay.Threads.Select(thread => new[]
        {
            thread.Thread,
            thread.Acquires.ToString(System.Globalization.CultureInfo.InvariantCulture),
            thread.Locks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            thread.MaxHeld.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

        return Table.Render(
            ["THREAD", "ACQUIRES", "LOCKS", "MAXHELD"],
            rows,
            [1, 2, 3]);
    }

    public static string Order(LockGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (!graph.TryGetOrder(out var order))
        {
            throw new ArgumentException("A cyclic lock graph has no lock order.", nameof(graph));
        }

        return string.Concat(order.Select(@lock => @lock + "\n"));
    }

    public static string NoOrderError(string display, LockGraph graph)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(graph);

        if (!graph.HasCycle)
        {
            throw new ArgumentException("The lock graph has no cycle.", nameof(graph));
        }

        return $"{display}: no lock order exists (cycle among {string.Join(", ", graph.CyclicLocks)})";
    }

    private static string FormatFinding(string display, Finding finding)
    {
        var message = finding switch
        {
            ReplayProblem problem => FormatReplayProblem(problem),
            InversionFinding inversion => FormatInversion(inversion),
            _ => throw new ArgumentOutOfRangeException(nameof(finding)),
        };

        return $"{display}:{finding.Line}: {message}\n";
    }

    private static string FormatReplayProblem(ReplayProblem problem)
    {
        var code = CodeText(problem.Code);
        return problem.Code switch
        {
            FindingCode.LK101 => $"{code} {problem.Thread} acquires {problem.Lock} again while holding it",
            FindingCode.LK102 => $"{code} {problem.Thread} acquires {problem.Lock} while {problem.Holder} holds it",
            FindingCode.LK103 => $"{code} {problem.Thread} releases {problem.Lock} without holding it",
            FindingCode.LK104 => $"{code} {problem.Thread} still holds {problem.Lock} at the end of the trace",
            _ => throw new ArgumentOutOfRangeException(nameof(problem)),
        };
    }

    private static string FormatInversion(InversionFinding inversion)
    {
        var output = new System.Text.StringBuilder();
        output.Append(CodeText(inversion.Code));
        output.Append(" lock order inversion among ");
        output.Append(string.Join(", ", inversion.Locks));
        output.Append(" (threads ");
        output.Append(string.Join(", ", inversion.Threads));
        output.Append(')');

        foreach (var edge in inversion.Edges)
        {
            output.Append("\n    ");
            output.Append(edge.From);
            output.Append(" -> ");
            output.Append(edge.To);
            output.Append(" (first at line ");
            output.Append(edge.FirstLine.ToString(System.Globalization.CultureInfo.InvariantCulture));
            output.Append(" by ");
            output.Append(edge.FirstThread);
            output.Append(')');
        }

        return output.ToString();
    }

    private static string CodeText(FindingCode code)
    {
        return $"LK{(int)code}";
    }
}
