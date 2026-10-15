namespace Lockorder.Core;

public sealed record LockEdge(
    string From,
    string To,
    int FirstLine,
    string FirstThread,
    IReadOnlyList<string> Threads);

public sealed record ThreadSummary(
    string Thread,
    int Acquires,
    int Locks,
    int MaxHeld);

public sealed class ReplayResult
{
    internal ReplayResult(
        IReadOnlyList<ReplayProblem> problems,
        IReadOnlyList<LockEdge> edges,
        IReadOnlyList<ThreadSummary> threads,
        IReadOnlyList<string> locks)
    {
        Problems = problems;
        Edges = edges;
        Threads = threads;
        Locks = locks;
    }

    public IReadOnlyList<ReplayProblem> Problems { get; }

    public IReadOnlyList<LockEdge> Edges { get; }

    public IReadOnlyList<ThreadSummary> Threads { get; }

    public IReadOnlyList<string> Locks { get; }
}

public static class Replay
{
    public static ReplayResult Run(
        IReadOnlyList<TraceEvent> events,
        IEnumerable<string>? ignoredLocks = null)
    {
        ArgumentNullException.ThrowIfNull(events);

        var ignored = ignoredLocks is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(ignoredLocks, StringComparer.Ordinal);
        var states = new Dictionary<string, ThreadState>(StringComparer.Ordinal);
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        var knownLocks = new HashSet<string>(StringComparer.Ordinal);
        var edgeStates = new Dictionary<(string From, string To), EdgeState>();
        var problems = new List<ReplayProblem>();

        foreach (var traceEvent in events)
        {
            if (ignored.Contains(traceEvent.Lock))
            {
                continue;
            }

            if (!states.TryGetValue(traceEvent.Thread, out var threadState))
            {
                threadState = new ThreadState();
                states.Add(traceEvent.Thread, threadState);
            }

            if (traceEvent.Verb == "acquire")
            {
                ProcessAcquire(traceEvent, threadState, owners, knownLocks, edgeStates, problems);
            }
            else
            {
                ProcessRelease(traceEvent, threadState, owners, problems);
            }
        }

        foreach (var (thread, state) in states)
        {
            foreach (var heldLock in state.Held)
            {
                problems.Add(new ReplayProblem(
                    FindingCode.LK104,
                    state.AcquireLines[heldLock],
                    thread,
                    heldLock));
            }
        }

        var edges = edgeStates.Values
            .Select(edge => new LockEdge(
                edge.From,
                edge.To,
                edge.FirstLine,
                edge.FirstThread,
                Array.AsReadOnly(edge.Threads.OrderBy(thread => thread, StringComparer.Ordinal).ToArray())))
            .OrderBy(edge => edge.From, StringComparer.Ordinal)
            .ThenBy(edge => edge.To, StringComparer.Ordinal)
            .ToArray();

        var threadSummaries = states
            .Select(pair => new ThreadSummary(
                pair.Key,
                pair.Value.Acquires,
                pair.Value.AcquiredLocks.Count,
                pair.Value.MaxHeld))
            .OrderBy(summary => summary.Thread, StringComparer.Ordinal)
            .ToArray();

        return new ReplayResult(
            Array.AsReadOnly(problems.ToArray()),
            Array.AsReadOnly(edges),
            Array.AsReadOnly(threadSummaries),
            Array.AsReadOnly(knownLocks.OrderBy(@lock => @lock, StringComparer.Ordinal).ToArray()));
    }

    private static void ProcessAcquire(
        TraceEvent traceEvent,
        ThreadState threadState,
        IDictionary<string, string> owners,
        ISet<string> knownLocks,
        IDictionary<(string From, string To), EdgeState> edges,
        ICollection<ReplayProblem> problems)
    {
        if (threadState.Held.Contains(traceEvent.Lock, StringComparer.Ordinal))
        {
            problems.Add(new ReplayProblem(
                FindingCode.LK101,
                traceEvent.Line,
                traceEvent.Thread,
                traceEvent.Lock));
            return;
        }

        if (owners.TryGetValue(traceEvent.Lock, out var holder))
        {
            problems.Add(new ReplayProblem(
                FindingCode.LK102,
                traceEvent.Line,
                traceEvent.Thread,
                traceEvent.Lock,
                holder));
            return;
        }

        foreach (var heldLock in threadState.Held)
        {
            var key = (heldLock, traceEvent.Lock);
            if (!edges.TryGetValue(key, out var edge))
            {
                edge = new EdgeState(heldLock, traceEvent.Lock, traceEvent.Line, traceEvent.Thread);
                edges.Add(key, edge);
            }

            edge.Threads.Add(traceEvent.Thread);
        }

        threadState.Held.Add(traceEvent.Lock);
        threadState.AcquireLines.Add(traceEvent.Lock, traceEvent.Line);
        threadState.Acquires++;
        threadState.AcquiredLocks.Add(traceEvent.Lock);
        threadState.MaxHeld = Math.Max(threadState.MaxHeld, threadState.Held.Count);
        owners.Add(traceEvent.Lock, traceEvent.Thread);
        knownLocks.Add(traceEvent.Lock);
    }

    private static void ProcessRelease(
        TraceEvent traceEvent,
        ThreadState threadState,
        IDictionary<string, string> owners,
        ICollection<ReplayProblem> problems)
    {
        var heldIndex = threadState.Held.FindIndex(
            heldLock => StringComparer.Ordinal.Equals(heldLock, traceEvent.Lock));
        if (heldIndex < 0)
        {
            problems.Add(new ReplayProblem(
                FindingCode.LK103,
                traceEvent.Line,
                traceEvent.Thread,
                traceEvent.Lock));
            return;
        }

        threadState.Held.RemoveAt(heldIndex);
        threadState.AcquireLines.Remove(traceEvent.Lock);
        owners.Remove(traceEvent.Lock);
    }

    private sealed class ThreadState
    {
        public List<string> Held { get; } = [];

        public Dictionary<string, int> AcquireLines { get; } = new(StringComparer.Ordinal);

        public HashSet<string> AcquiredLocks { get; } = new(StringComparer.Ordinal);

        public int Acquires { get; set; }

        public int MaxHeld { get; set; }
    }

    private sealed class EdgeState
    {
        public EdgeState(string from, string to, int firstLine, string firstThread)
        {
            From = from;
            To = to;
            FirstLine = firstLine;
            FirstThread = firstThread;
        }

        public string From { get; }

        public string To { get; }

        public int FirstLine { get; }

        public string FirstThread { get; }

        public HashSet<string> Threads { get; } = new(StringComparer.Ordinal);
    }
}
