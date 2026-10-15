namespace Lockorder.Core;

public sealed class LockGraph
{
    private readonly IReadOnlyList<string> locks;
    private readonly Dictionary<string, IReadOnlyList<string>> outgoing;

    public LockGraph(ReplayResult replay)
    {
        ArgumentNullException.ThrowIfNull(replay);

        locks = replay.Locks.OrderBy(@lock => @lock, StringComparer.Ordinal).ToArray();
        var edges = replay.Edges;
        var neighbors = locks.ToDictionary(
            @lock => @lock,
            _ => new SortedSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            neighbors[edge.From].Add(edge.To);
        }

        outgoing = neighbors.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
            StringComparer.Ordinal);
        StronglyConnectedComponents = Array.AsReadOnly(FindComponents().ToArray());
        CyclicLocks = Array.AsReadOnly(
            StronglyConnectedComponents
                .Where(component => component.Count > 1)
                .SelectMany(component => component)
                .OrderBy(@lock => @lock, StringComparer.Ordinal)
                .ToArray());
    }

    public IReadOnlyList<IReadOnlyList<string>> StronglyConnectedComponents { get; }

    public IReadOnlyList<string> CyclicLocks { get; }

    public bool HasCycle => CyclicLocks.Count > 0;

    public bool TryGetOrder(out IReadOnlyList<string> order)
    {
        var indegrees = locks.ToDictionary(@lock => @lock, _ => 0, StringComparer.Ordinal);
        foreach (var destinations in outgoing.Values)
        {
            foreach (var destination in destinations)
            {
                indegrees[destination]++;
            }
        }

        var available = new SortedSet<string>(
            indegrees.Where(pair => pair.Value == 0).Select(pair => pair.Key),
            StringComparer.Ordinal);
        var result = new List<string>(locks.Count);

        while (available.Count > 0)
        {
            var next = available.Min!;
            available.Remove(next);
            result.Add(next);

            foreach (var destination in outgoing[next])
            {
                indegrees[destination]--;
                if (indegrees[destination] == 0)
                {
                    available.Add(destination);
                }
            }
        }

        if (result.Count != locks.Count)
        {
            order = Array.Empty<string>();
            return false;
        }

        order = Array.AsReadOnly(result.ToArray());
        return true;
    }

    private IEnumerable<IReadOnlyList<string>> FindComponents()
    {
        var nextIndex = 0;
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLinks = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<IReadOnlyList<string>>();

        void Visit(string @lock)
        {
            indices.Add(@lock, nextIndex);
            lowLinks.Add(@lock, nextIndex);
            nextIndex++;
            stack.Push(@lock);
            onStack.Add(@lock);

            foreach (var neighbor in outgoing[@lock])
            {
                if (!indices.ContainsKey(neighbor))
                {
                    Visit(neighbor);
                    lowLinks[@lock] = Math.Min(lowLinks[@lock], lowLinks[neighbor]);
                }
                else if (onStack.Contains(neighbor))
                {
                    lowLinks[@lock] = Math.Min(lowLinks[@lock], indices[neighbor]);
                }
            }

            if (lowLinks[@lock] != indices[@lock])
            {
                return;
            }

            var component = new List<string>();
            string member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (!StringComparer.Ordinal.Equals(member, @lock));

            component.Sort(StringComparer.Ordinal);
            components.Add(Array.AsReadOnly(component.ToArray()));
        }

        foreach (var @lock in locks)
        {
            if (!indices.ContainsKey(@lock))
            {
                Visit(@lock);
            }
        }

        return components
            .OrderBy(component => component[0], StringComparer.Ordinal)
            .ToArray();
    }
}
