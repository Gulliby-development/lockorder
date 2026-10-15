using Lockorder.Core;

namespace Lockorder.Tests;

public sealed class LockGraphTests
{
    [Fact]
    public void EmptyReplayHasNoComponentsAndAnEmptyOrder()
    {
        var graph = new LockGraph(Replay.Run(Array.Empty<TraceEvent>()));

        Assert.Empty(graph.StronglyConnectedComponents);
        Assert.Empty(graph.CyclicLocks);
        Assert.False(graph.HasCycle);
        Assert.True(graph.TryGetOrder(out var order));
        Assert.Empty(order);
    }

    [Fact]
    public void AcyclicOrderChoosesTheSmallestCurrentlyEligibleLock()
    {
        var graph = CreateGraph(
            @"
            t acquire z
            t acquire a
            t release a
            t release z
            t acquire b
            t release b
            t acquire c
            t release c
            ");

        Assert.True(graph.TryGetOrder(out var order));
        Assert.Equal(new[] { "b", "c", "z", "a" }, order);
        Assert.False(graph.HasCycle);
    }

    [Fact]
    public void IsolatedKnownLocksAreIncludedInOrdinalOrder()
    {
        var graph = CreateGraph(
            @"
            t acquire z
            t release z
            t acquire A
            t release A
            t acquire a
            t release a
            ");

        Assert.True(graph.TryGetOrder(out var order));
        Assert.Equal(new[] { "A", "a", "z" }, order);
        Assert.Equal(new[] { "A", "a", "z" }, graph.StronglyConnectedComponents.SelectMany(component => component));
    }

    [Fact]
    public void ASingleEdgeIsAcyclicAndPreservesItsRequiredOrder()
    {
        var graph = CreateGraph(
            @"
            t acquire before
            t acquire after
            t release after
            t release before
            ");

        Assert.False(graph.HasCycle);
        Assert.Empty(graph.CyclicLocks);
        Assert.True(graph.TryGetOrder(out var order));
        Assert.Equal(new[] { "before", "after" }, order);
        Assert.Equal(
            new[] { new[] { "after" }, new[] { "before" } },
            graph.StronglyConnectedComponents);
    }

    [Fact]
    public void TwoLockCycleIsOneSortedComponentAndHasNoOrder()
    {
        var graph = CreateGraph(
            @"
            t1 acquire b
            t1 acquire a
            t1 release a
            t1 release b
            t2 acquire a
            t2 acquire b
            t2 release b
            t2 release a
            ");

        Assert.True(graph.HasCycle);
        Assert.Equal(new[] { "a", "b" }, graph.CyclicLocks);
        Assert.Equal(new[] { new[] { "a", "b" } }, graph.StronglyConnectedComponents);
        Assert.False(graph.TryGetOrder(out var order));
        Assert.Empty(order);
    }

    [Fact]
    public void ThreeLockCycleIsOneSortedComponent()
    {
        var graph = CreateGraph(
            @"
            t1 acquire c
            t1 acquire a
            t1 release a
            t1 release c
            t2 acquire a
            t2 acquire b
            t2 release b
            t2 release a
            t3 acquire b
            t3 acquire c
            t3 release c
            t3 release b
            ");

        Assert.True(graph.HasCycle);
        Assert.Equal(new[] { "a", "b", "c" }, graph.CyclicLocks);
        Assert.Equal(new[] { new[] { "a", "b", "c" } }, graph.StronglyConnectedComponents);
    }

    [Fact]
    public void ACycleRecordedByOneThreadStillPreventsAnOrder()
    {
        var graph = CreateGraph(
            @"
            t acquire a
            t acquire b
            t release b
            t release a
            t acquire b
            t acquire a
            t release a
            t release b
            ");

        Assert.True(graph.HasCycle);
        Assert.Equal(new[] { "a", "b" }, graph.CyclicLocks);
        Assert.False(graph.TryGetOrder(out _));
    }

    [Fact]
    public void DisjointCyclesProduceTheirSortedUnion()
    {
        var graph = CreateGraph(
            @"
            t1 acquire b
            t1 acquire a
            t1 release a
            t1 release b
            t2 acquire a
            t2 acquire b
            t2 release b
            t2 release a
            t3 acquire y
            t3 acquire x
            t3 release x
            t3 release y
            t4 acquire x
            t4 acquire y
            t4 release y
            t4 release x
            ");

        Assert.True(graph.HasCycle);
        Assert.Equal(new[] { "a", "b", "x", "y" }, graph.CyclicLocks);
        Assert.Equal(
            new[] { new[] { "a", "b" }, new[] { "x", "y" } },
            graph.StronglyConnectedComponents);
    }

    [Fact]
    public void ComponentsAreSortedByTheirFirstLockAndIncludeAcyclicLocks()
    {
        var graph = CreateGraph(
            @"
            t1 acquire b
            t1 acquire a
            t1 release a
            t1 release b
            t2 acquire a
            t2 acquire b
            t2 release b
            t2 release a
            t3 acquire x
            t3 acquire y
            t3 release y
            t3 release x
            t4 acquire y
            t4 acquire x
            t4 release x
            t4 release y
            t5 acquire m
            t5 release m
            ");

        Assert.Equal(
            new[] { new[] { "a", "b" }, new[] { "m" }, new[] { "x", "y" } },
            graph.StronglyConnectedComponents);
        Assert.Equal(new[] { "a", "b", "x", "y" }, graph.CyclicLocks);
    }

    [Fact]
    public void EdgeBetweenCyclesDoesNotMergeTheirComponents()
    {
        var graph = CreateGraph(
            @"
            t1 acquire a
            t1 acquire b
            t1 release b
            t1 release a
            t2 acquire b
            t2 acquire a
            t2 release a
            t2 release b
            t3 acquire b
            t3 acquire c
            t3 release c
            t3 release b
            t4 acquire c
            t4 acquire d
            t4 release d
            t4 release c
            t5 acquire d
            t5 acquire c
            t5 release c
            t5 release d
            ");

        Assert.Equal(
            new[] { new[] { "a", "b" }, new[] { "c", "d" } },
            graph.StronglyConnectedComponents);
        Assert.Equal(new[] { "a", "b", "c", "d" }, graph.CyclicLocks);
        Assert.False(graph.TryGetOrder(out _));
    }

    [Fact]
    public void CycleAndIsolatedLockKeepTheIsolatedLockOutsideCycleUnion()
    {
        var graph = CreateGraph(
            @"
            t1 acquire b
            t1 acquire a
            t1 release a
            t1 release b
            t2 acquire a
            t2 acquire b
            t2 release b
            t2 release a
            t3 acquire isolated
            t3 release isolated
            ");

        Assert.True(graph.HasCycle);
        Assert.Equal(new[] { "a", "b" }, graph.CyclicLocks);
        Assert.Equal(
            new[] { new[] { "a", "b" }, new[] { "isolated" } },
            graph.StronglyConnectedComponents);
    }

    private static LockGraph CreateGraph(string trace)
    {
        var events = Trace.Parse("graph.log", trace);
        return new LockGraph(Replay.Run(events));
    }
}
