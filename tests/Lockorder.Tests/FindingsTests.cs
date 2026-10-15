using Lockorder.Core;

namespace Lockorder.Tests;

public sealed class FindingsTests
{
    [Fact]
    public void BadTraceReportsAllReplayProblemCodesInLineOrder()
    {
        var analysis = Analyze(
            "t1 acquire a\n" +
            "t1 acquire a\n" +
            "t2 acquire a\n" +
            "t2 release b\n" +
            "t1 release a\n" +
            "t3 acquire c\n");

        Assert.Equal(
            new[]
            {
                (FindingCode.LK101, 2),
                (FindingCode.LK102, 3),
                (FindingCode.LK103, 4),
                (FindingCode.LK104, 6),
            },
            analysis.Findings.Select(finding => (finding.Code, finding.Line)));

        Assert.Equal(
            "trace.log:2: LK101 t1 acquires a again while holding it\n" +
            "trace.log:3: LK102 t2 acquires a while t1 holds it\n" +
            "trace.log:4: LK103 t2 releases b without holding it\n" +
            "trace.log:6: LK104 t3 still holds c at the end of the trace\n",
            Report.Check("trace.log", analysis.Replay, analysis.Graph));
    }

    [Fact]
    public void ReacquiringAnOwnedLockIsLK101AtTheRejectedEventLine()
    {
        var analysis = Analyze(
            "worker acquire gate\n" +
            "worker acquire gate\n" +
            "worker release gate\n");

        var problem = Assert.IsType<ReplayProblem>(Assert.Single(analysis.Findings));
        Assert.Equal(FindingCode.LK101, problem.Code);
        Assert.Equal(2, problem.Line);
        Assert.Equal("worker", problem.Thread);
        Assert.Equal("gate", problem.Lock);
    }

    [Fact]
    public void AcquiringAnotherThreadsLockIsLK102AndNamesItsHolder()
    {
        var analysis = Analyze(
            "owner acquire gate\n" +
            "other acquire gate\n" +
            "owner release gate\n");

        var problem = Assert.IsType<ReplayProblem>(Assert.Single(analysis.Findings));
        Assert.Equal(FindingCode.LK102, problem.Code);
        Assert.Equal(2, problem.Line);
        Assert.Equal("other", problem.Thread);
        Assert.Equal("gate", problem.Lock);
        Assert.Equal("owner", problem.Holder);
    }

    [Fact]
    public void AThreadNamedLikeAFindingCodeRemainsANameInTheExactReport()
    {
        var analysis = Analyze("LK103 acquire a\nLK103 release b\n");

        Assert.Equal(
            new[]
            {
                (FindingCode.LK104, 1),
                (FindingCode.LK103, 2),
            },
            analysis.Findings.Select(finding => (finding.Code, finding.Line)));
        Assert.Equal(
            "trace.log:1: LK104 LK103 still holds a at the end of the trace\n" +
            "trace.log:2: LK103 LK103 releases b without holding it\n",
            Report.Check("trace.log", analysis.Replay, analysis.Graph));
    }

    [Fact]
    public void EachUnreleasedLockUsesItsOwnAcquireLineForLK104()
    {
        var analysis = Analyze(
            "worker acquire zeta\n" +
            "worker acquire alpha\n");

        var problems = analysis.Findings.Cast<ReplayProblem>().ToArray();
        Assert.Collection(
            problems,
            problem =>
            {
                Assert.Equal(FindingCode.LK104, problem.Code);
                Assert.Equal(1, problem.Line);
                Assert.Equal("zeta", problem.Lock);
            },
            problem =>
            {
                Assert.Equal(FindingCode.LK104, problem.Code);
                Assert.Equal(2, problem.Line);
                Assert.Equal("alpha", problem.Lock);
            });
    }

    [Fact]
    public void FindingsWithTheSameLineAreOrderedByCodeBeforeRendering()
    {
        var events = new[]
        {
            new TraceEvent(1, "t1", "acquire", "a"),
            new TraceEvent(2, "t1", "acquire", "b"),
            new TraceEvent(3, "t1", "release", "b"),
            new TraceEvent(4, "t1", "release", "a"),
            new TraceEvent(5, "t2", "acquire", "b"),
            new TraceEvent(2, "t2", "acquire", "a"),
        };
        var replay = Replay.Run(events);
        var graph = new LockGraph(replay);
        var findings = Findings.Create(replay, graph);

        Assert.Equal(
            new[]
            {
                (2, FindingCode.LK104),
                (2, FindingCode.LK201),
                (5, FindingCode.LK104),
            },
            findings.Select(finding => (finding.Line, finding.Code)));
        Assert.Equal(
            "trace.log:2: LK104 t2 still holds a at the end of the trace\n" +
            "trace.log:2: LK201 lock order inversion among a, b (threads t1, t2)\n" +
            "    a -> b (first at line 2 by t1)\n" +
            "    b -> a (first at line 2 by t2)\n" +
            "trace.log:5: LK104 t2 still holds b at the end of the trace\n",
            Report.Check("trace.log", replay, graph));
    }

    [Fact]
    public void TwoLockInversionUsesItsEarliestEdgeLineAndSortedDetails()
    {
        var analysis = Analyze(
            "first acquire a\n" +
            "first acquire b\n" +
            "first release b\n" +
            "first release a\n" +
            "second acquire b\n" +
            "second acquire a\n" +
            "second release a\n" +
            "second release b\n");

        var inversion = Assert.IsType<InversionFinding>(Assert.Single(analysis.Findings));
        Assert.Equal(2, inversion.Line);
        Assert.Equal(new[] { "a", "b" }, inversion.Locks);
        Assert.Equal(new[] { "first", "second" }, inversion.Threads);
        Assert.Equal(
            "trace.log:2: LK201 lock order inversion among a, b (threads first, second)\n" +
            "    a -> b (first at line 2 by first)\n" +
            "    b -> a (first at line 6 by second)\n",
            Report.Check("trace.log", analysis.Replay, analysis.Graph));
    }

    [Fact]
    public void ThreeLockInversionIncludesEveryInternalEdgeInCodePointOrder()
    {
        var analysis = Analyze(
            "first acquire a\n" +
            "first acquire b\n" +
            "first acquire c\n" +
            "first release c\n" +
            "first release b\n" +
            "first release a\n" +
            "second acquire c\n" +
            "second acquire b\n" +
            "second acquire a\n" +
            "second release a\n" +
            "second release b\n" +
            "second release c\n");

        var inversion = Assert.IsType<InversionFinding>(Assert.Single(analysis.Findings));
        Assert.Equal(new[] { "a", "b", "c" }, inversion.Locks);
        Assert.Equal(new[] { "first", "second" }, inversion.Threads);
        Assert.Equal(
            new[]
            {
                ("a", "b"),
                ("a", "c"),
                ("b", "a"),
                ("b", "c"),
                ("c", "a"),
                ("c", "b"),
            },
            inversion.Edges.Select(edge => (edge.From, edge.To)));
    }

    [Fact]
    public void ASingleThreadCycleIsNotAnInversionFinding()
    {
        var analysis = Analyze(
            "worker acquire a\n" +
            "worker acquire b\n" +
            "worker release b\n" +
            "worker release a\n" +
            "worker acquire b\n" +
            "worker acquire a\n" +
            "worker release a\n" +
            "worker release b\n");

        Assert.True(analysis.Graph.HasCycle);
        Assert.Empty(analysis.Findings);
    }

    [Fact]
    public void SeparateCyclicComponentsProduceSeparateFindingsWithLocalThreads()
    {
        var analysis = Analyze(
            "ab1 acquire a\n" +
            "ab1 acquire b\n" +
            "ab1 release b\n" +
            "ab1 release a\n" +
            "ab2 acquire b\n" +
            "ab2 acquire a\n" +
            "ab2 release a\n" +
            "ab2 release b\n" +
            "cd1 acquire c\n" +
            "cd1 acquire d\n" +
            "cd1 release d\n" +
            "cd1 release c\n" +
            "cd2 acquire d\n" +
            "cd2 acquire c\n" +
            "cd2 release c\n" +
            "cd2 release d\n");

        var inversions = analysis.Findings.Cast<InversionFinding>().ToArray();
        Assert.Equal(2, inversions.Length);
        Assert.Equal(new[] { "a", "b" }, inversions[0].Locks);
        Assert.Equal(new[] { "ab1", "ab2" }, inversions[0].Threads);
        Assert.Equal(2, inversions[0].Line);
        Assert.Equal(new[] { "c", "d" }, inversions[1].Locks);
        Assert.Equal(new[] { "cd1", "cd2" }, inversions[1].Threads);
        Assert.Equal(10, inversions[1].Line);
    }

    [Fact]
    public void InversionThreadListIncludesEveryInternalEdgeRecorderOnce()
    {
        var analysis = Analyze(
            "alpha acquire a\n" +
            "alpha acquire b\n" +
            "alpha release b\n" +
            "alpha release a\n" +
            "beta acquire a\n" +
            "beta acquire b\n" +
            "beta release b\n" +
            "beta release a\n" +
            "gamma acquire b\n" +
            "gamma acquire a\n" +
            "gamma release a\n" +
            "gamma release b\n");

        var inversion = Assert.IsType<InversionFinding>(Assert.Single(analysis.Findings));
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, inversion.Threads);
        Assert.Equal(2, inversion.Line);
        Assert.Equal("alpha", inversion.Edges.Single(edge => edge.From == "a").FirstThread);
        Assert.Equal(
            new[] { "alpha", "beta" },
            inversion.Edges.Single(edge => edge.From == "a").Threads);
    }

    private static (ReplayResult Replay, LockGraph Graph, IReadOnlyList<Finding> Findings) Analyze(string text)
    {
        var replay = Replay.Run(Trace.Parse("trace.log", text));
        var graph = new LockGraph(replay);
        return (replay, graph, Findings.Create(replay, graph));
    }
}
