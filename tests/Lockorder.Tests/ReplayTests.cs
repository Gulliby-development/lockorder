using Lockorder.Core;

namespace Lockorder.Tests;

public sealed class ReplayTests
{
    [Fact]
    public void IgnoredLockIsSkippedCompletely()
    {
        var result = Run(
            "T acquire ignored\nT release ignored\nT acquire kept\nT release kept",
            "ignored");

        Assert.Equal(["kept"], result.Locks);
        Assert.Empty(result.Edges);
        Assert.Empty(result.Problems);
        Assert.Collection(
            result.Threads,
            thread =>
            {
                Assert.Equal("T", thread.Thread);
                Assert.Equal(1, thread.Acquires);
                Assert.Equal(1, thread.Locks);
                Assert.Equal(1, thread.MaxHeld);
            });
    }

    [Fact]
    public void IgnoredOnlyEventsProduceNoLocksOrThreads()
    {
        var result = Run("T acquire ignored\nU release ignored", "ignored");

        Assert.Empty(result.Locks);
        Assert.Empty(result.Threads);
        Assert.Empty(result.Edges);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void ReacquireIsRejectedWithoutChangingOwnershipOrStatistics()
    {
        var result = Run("T acquire A\nT acquire A\nT acquire B");

        Assert.Collection(
            result.Problems,
            problem =>
            {
                Assert.Equal(FindingCode.LK101, problem.Code);
                Assert.Equal(2, problem.Line);
                Assert.Equal("T", ((ReplayProblem)problem).Thread);
                Assert.Equal("A", ((ReplayProblem)problem).Lock);
            },
            firstHeld =>
            {
                Assert.Equal(FindingCode.LK104, firstHeld.Code);
                Assert.Equal(1, firstHeld.Line);
                Assert.Equal("T", ((ReplayProblem)firstHeld).Thread);
                Assert.Equal("A", ((ReplayProblem)firstHeld).Lock);
            },
            secondHeld =>
            {
                Assert.Equal(FindingCode.LK104, secondHeld.Code);
                Assert.Equal(3, secondHeld.Line);
                Assert.Equal("T", ((ReplayProblem)secondHeld).Thread);
                Assert.Equal("B", ((ReplayProblem)secondHeld).Lock);
            });
        Assert.Equal(["A", "B"], result.Locks);
        Assert.Collection(
            result.Edges,
            edge =>
            {
                Assert.Equal("A", edge.From);
                Assert.Equal("B", edge.To);
                Assert.Equal(3, edge.FirstLine);
            });
        var thread = Assert.Single(result.Threads);
        Assert.Equal(2, thread.Acquires);
        Assert.Equal(2, thread.Locks);
        Assert.Equal(2, thread.MaxHeld);
    }

    [Fact]
    public void LockHeldByAnotherThreadIsRejectedWithoutCountingAcquire()
    {
        var result = Run("T acquire A\nU acquire A\nT release A");

        var problem = Assert.Single(result.Problems);
        Assert.Equal(FindingCode.LK102, problem.Code);
        Assert.Equal(2, problem.Line);
        Assert.Equal("U", ((ReplayProblem)problem).Thread);
        Assert.Equal("T", ((ReplayProblem)problem).Holder);
        Assert.Empty(result.Edges);
        Assert.Collection(
            result.Threads,
            first =>
            {
                Assert.Equal("T", first.Thread);
                Assert.Equal(1, first.Acquires);
                Assert.Equal(1, first.Locks);
                Assert.Equal(1, first.MaxHeld);
            },
            second =>
            {
                Assert.Equal("U", second.Thread);
                Assert.Equal(0, second.Acquires);
                Assert.Equal(0, second.Locks);
                Assert.Equal(0, second.MaxHeld);
            });
        Assert.Equal(["A"], result.Locks);
    }

    [Fact]
    public void NonHolderReleaseDoesNotReleaseAnotherThreadsLock()
    {
        var result = Run("T acquire A\nU release A\nT release A");

        var problem = Assert.Single(result.Problems);
        Assert.Equal(FindingCode.LK103, problem.Code);
        Assert.Equal(2, problem.Line);
        Assert.Equal("U", ((ReplayProblem)problem).Thread);
        Assert.Empty(result.Edges);
        Assert.Collection(
            result.Threads,
            holder =>
            {
                Assert.Equal("T", holder.Thread);
                Assert.Equal(1, holder.Acquires);
                Assert.Equal(1, holder.MaxHeld);
            },
            releaser =>
            {
                Assert.Equal("U", releaser.Thread);
                Assert.Equal(0, releaser.Acquires);
                Assert.Equal(0, releaser.Locks);
                Assert.Equal(0, releaser.MaxHeld);
            });
    }

    [Fact]
    public void ReleaseByThreadWithNoHeldLocksIsReportedAndThreadRemainsInSummary()
    {
        var result = Run("T release absent");

        var problem = Assert.Single(result.Problems);
        Assert.Equal(FindingCode.LK103, problem.Code);
        Assert.Equal(1, problem.Line);
        Assert.Equal("absent", ((ReplayProblem)problem).Lock);
        Assert.Empty(result.Locks);
        Assert.Empty(result.Edges);
        var thread = Assert.Single(result.Threads);
        Assert.Equal("T", thread.Thread);
        Assert.Equal(0, thread.Acquires);
        Assert.Equal(0, thread.Locks);
        Assert.Equal(0, thread.MaxHeld);
    }

    [Fact]
    public void LocksMayBeReleasedOutOfAcquisitionOrder()
    {
        var result = Run("T acquire A\nT acquire B\nT release A\nT release B");

        Assert.Empty(result.Problems);
        Assert.Equal(["A", "B"], result.Locks);
        var edge = Assert.Single(result.Edges);
        Assert.Equal(("A", "B", 2, "T"), (edge.From, edge.To, edge.FirstLine, edge.FirstThread));
        var thread = Assert.Single(result.Threads);
        Assert.Equal(2, thread.Acquires);
        Assert.Equal(2, thread.Locks);
        Assert.Equal(2, thread.MaxHeld);
    }

    [Fact]
    public void AcceptedAcquireAddsEdgesFromEveryCurrentlyHeldLock()
    {
        var result = Run("T acquire A\nT acquire B\nT acquire C");

        Assert.Collection(
            result.Edges,
            edge => Assert.Equal(("A", "B", 2), (edge.From, edge.To, edge.FirstLine)),
            edge => Assert.Equal(("A", "C", 3), (edge.From, edge.To, edge.FirstLine)),
            edge => Assert.Equal(("B", "C", 3), (edge.From, edge.To, edge.FirstLine)));
        Assert.Equal(3, Assert.Single(result.Threads).MaxHeld);
    }

    [Fact]
    public void EdgeKeepsEarliestProvenanceAndDeduplicatesRecordingThread()
    {
        var result = Run(
            "t1 acquire A\nt1 acquire B\nt1 release A\nt1 release B\n"
            + "t1 acquire A\nt1 acquire B\nt1 release B\nt1 release A\n"
            + "t2 acquire A\nt2 acquire B");

        var edge = Assert.Single(result.Edges);
        Assert.Equal("A", edge.From);
        Assert.Equal("B", edge.To);
        Assert.Equal(2, edge.FirstLine);
        Assert.Equal("t1", edge.FirstThread);
        Assert.Equal(["t1", "t2"], edge.Threads);
    }

    [Fact]
    public void EdgesAreOrderedBySourceThenDestinationUsingOrdinalNames()
    {
        var result = Run(
            "T acquire z\nT acquire b\nT release b\nT release z\n"
            + "T acquire a\nT acquire c");

        Assert.Equal(
            [("a", "c"), ("z", "b")],
            result.Edges.Select(edge => (edge.From, edge.To)));
    }

    [Fact]
    public void FinalHeldLocksReportTheirOwnAcquireLines()
    {
        var result = Run("T acquire A\nT acquire B\nT release A\nU acquire C");

        Assert.Collection(
            result.Problems.OrderBy(problem => problem.Line),
            first =>
            {
                Assert.Equal(FindingCode.LK104, first.Code);
                Assert.Equal(2, first.Line);
                Assert.Equal("T", ((ReplayProblem)first).Thread);
                Assert.Equal("B", ((ReplayProblem)first).Lock);
            },
            second =>
            {
                Assert.Equal(FindingCode.LK104, second.Code);
                Assert.Equal(4, second.Line);
                Assert.Equal("U", ((ReplayProblem)second).Thread);
                Assert.Equal("C", ((ReplayProblem)second).Lock);
            });
    }

    [Fact]
    public void ReacquiringAfterReleaseCountsAcceptedAcquireButOnlyDistinctLock()
    {
        var result = Run("T acquire A\nT release A\nT acquire A\nT release A");

        Assert.Empty(result.Problems);
        Assert.Equal(["A"], result.Locks);
        var thread = Assert.Single(result.Threads);
        Assert.Equal(2, thread.Acquires);
        Assert.Equal(1, thread.Locks);
        Assert.Equal(1, thread.MaxHeld);
    }

    [Fact]
    public void RejectedAndReleaseOnlyThreadsAreIncludedInOrdinalThreadOrder()
    {
        var result = Run("z release absent\na acquire A\nb acquire A\na release A");

        Assert.Equal(["a", "b", "z"], result.Threads.Select(thread => thread.Thread));
        Assert.Equal([1, 0, 0], result.Threads.Select(thread => thread.Acquires));
        Assert.Collection(
            result.Problems,
            first => Assert.Equal(FindingCode.LK103, first.Code),
            second => Assert.Equal(FindingCode.LK102, second.Code));
    }

    [Fact]
    public void FailedAcquireDoesNotAddAnEdgeOrChangeTheOriginalHolder()
    {
        var result = Run(
            "T acquire A\nT acquire B\nU acquire A\nT release B\nT release A");

        var problem = Assert.Single(result.Problems);
        Assert.Equal(FindingCode.LK102, problem.Code);
        Assert.Equal(["A", "B"], result.Locks);
        var edge = Assert.Single(result.Edges);
        Assert.Equal(("A", "B", 2), (edge.From, edge.To, edge.FirstLine));
        Assert.Equal(2, Assert.Single(result.Threads, thread => thread.Thread == "T").Acquires);
        Assert.Equal(0, Assert.Single(result.Threads, thread => thread.Thread == "U").Acquires);
    }

    [Theory]
    [InlineData("X acquire A\nX release A", 1, 1, 1)]
    [InlineData("X acquire A\nX acquire B\nX release B\nX release A", 2, 2, 2)]
    [InlineData("X acquire A\nX release A\nX acquire B\nX release B", 2, 2, 1)]
    public void AcceptedAcquireSummaryCountsAcceptedEventsDistinctLocksAndPeak(
        string text,
        int acquires,
        int locks,
        int maxHeld)
    {
        var result = Run(text);

        Assert.Empty(result.Problems);
        var thread = Assert.Single(result.Threads);
        Assert.Equal(acquires, thread.Acquires);
        Assert.Equal(locks, thread.Locks);
        Assert.Equal(maxHeld, thread.MaxHeld);
    }

    [Theory]
    [InlineData("T acquire A\nU release A", FindingCode.LK104, 1)]
    [InlineData("T acquire A\nU acquire A", FindingCode.LK102, 2)]
    [InlineData("T acquire A\nT acquire A", FindingCode.LK101, 2)]
    public void ProblemsRetainTheRelevantEventOrAcquireLine(string text, FindingCode code, int line)
    {
        var result = Run(text);

        Assert.Contains(result.Problems, problem => problem.Code == code && problem.Line == line);
    }

    private static ReplayResult Run(string text, params string[] ignoredLocks)
    {
        return Replay.Run(Trace.Parse("trace.log", text), ignoredLocks);
    }
}
