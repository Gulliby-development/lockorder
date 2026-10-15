namespace Lockorder.Core;

public enum FindingCode
{
    LK101 = 101,
    LK102 = 102,
    LK103 = 103,
    LK104 = 104,
    LK201 = 201
}

public abstract class Finding
{
    protected Finding(FindingCode code, int line)
    {
        Code = code;
        Line = line;
    }

    public FindingCode Code { get; }

    public int Line { get; }
}

public sealed class ReplayProblem : Finding
{
    public ReplayProblem(
        FindingCode code,
        int line,
        string thread,
        string @lock,
        string? holder = null)
        : base(code, line)
    {
        if (code is FindingCode.LK201)
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }

        Thread = thread;
        Lock = @lock;
        Holder = holder;
    }

    public string Thread { get; }

    public string Lock { get; }

    public string? Holder { get; }
}

public sealed class InversionFinding : Finding
{
    public InversionFinding(
        int line,
        IReadOnlyList<string> locks,
        IReadOnlyList<string> threads,
        IReadOnlyList<LockEdge> edges)
        : base(FindingCode.LK201, line)
    {
        Locks = locks;
        Threads = threads;
        Edges = edges;
    }

    public IReadOnlyList<string> Locks { get; }

    public IReadOnlyList<string> Threads { get; }

    public IReadOnlyList<LockEdge> Edges { get; }
}

public static class Findings
{
    public static IReadOnlyList<Finding> Create(ReplayResult replay, LockGraph graph)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(graph);

        var findings = new List<Finding>(replay.Problems);
        foreach (var component in graph.StronglyConnectedComponents)
        {
            if (component.Count < 2)
            {
                continue;
            }

            var members = new HashSet<string>(component, StringComparer.Ordinal);
            var internalEdges = replay.Edges
                .Where(edge => members.Contains(edge.From) && members.Contains(edge.To))
                .OrderBy(edge => edge.From, StringComparer.Ordinal)
                .ThenBy(edge => edge.To, StringComparer.Ordinal)
                .ToArray();
            var threads = internalEdges
                .SelectMany(edge => edge.Threads)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(thread => thread, StringComparer.Ordinal)
                .ToArray();

            if (threads.Length < 2)
            {
                continue;
            }

            findings.Add(new InversionFinding(
                internalEdges.Min(edge => edge.FirstLine),
                Array.AsReadOnly(component.ToArray()),
                Array.AsReadOnly(threads),
                Array.AsReadOnly(internalEdges)));
        }

        return Array.AsReadOnly(
            findings
                .OrderBy(finding => finding.Line)
                .ThenBy(finding => finding.Code)
                .ToArray());
    }
}
