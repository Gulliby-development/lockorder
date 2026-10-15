using Lockorder.Core;

namespace Lockorder.Tests;

public class TraceTests
{
    [Fact]
    public void ParsesSpaceAndTabSeparatedEvents()
    {
        var events = Trace.Parse("trace.log", "worker-1   acquire\tdb.pool\nworker-1\trelease  db.pool");

        Assert.Equal(
            new[]
            {
                new TraceEvent(1, "worker-1", "acquire", "db.pool"),
                new TraceEvent(2, "worker-1", "release", "db.pool"),
            },
            events);
    }

    [Theory]
    [InlineData("T acquire L", 1)]
    [InlineData("  T acquire L  ", 1)]
    [InlineData("\tT\tacquire\tL\t", 1)]
    [InlineData("T  acquire\tL", 1)]
    public void AcceptsSpaceAndTabSeparatorsAndTrimming(string line, int expectedLine)
    {
        var events = Trace.Parse("trace.log", line);

        Assert.Equal(new TraceEvent(expectedLine, "T", "acquire", "L"), Assert.Single(events));
    }

    [Theory]
    [InlineData("T acquire")]
    [InlineData("T acquire L extra")]
    [InlineData("T acquire L # inline comment")]
    [InlineData("T")]
    public void ReportsWrongTokenCountBeforeTokenValidation(string line)
    {
        AssertLineError(line, 1, "expected \"THREAD acquire|release LOCK\"");
    }

    [Theory]
    [InlineData("T lock L", "lock")]
    [InlineData("T ACQUIRE L", "ACQUIRE")]
    [InlineData("1t nonsense 2l", "nonsense")]
    public void ReportsInvalidVerbBeforeInvalidNames(string line, string verb)
    {
        AssertLineError(
            line,
            1,
            $"invalid verb \"{verb}\" (expected acquire or release)");
    }

    [Theory]
    [InlineData("1t acquire 2l", "1t")]
    [InlineData("acquire acquire L", "acquire")]
    [InlineData("t🙂 acquire L", "t?")]
    public void ReportsInvalidThreadNamesBeforeInvalidLockNames(string line, string thread)
    {
        AssertLineError(line, 1, $"invalid thread name \"{thread}\"");
    }

    [Theory]
    [InlineData("T acquire 1l", "1l")]
    [InlineData("T acquire release", "release")]
    [InlineData("T acquire ab?", "ab?")]
    public void ReportsInvalidLockNames(string line, string @lock)
    {
        AssertLineError(line, 1, $"invalid lock name \"{@lock}\"");
    }

    [Fact]
    public void ReportsFirstBadLineUsingPhysicalLineNumbers()
    {
        AssertLineError(
            "T acquire L\n# comment\n\t\nT lock L\nT acquire 1l",
            4,
            "invalid verb \"lock\" (expected acquire or release)");
    }

    [Fact]
    public void SkipsBlankAndCommentLinesAfterTrimmingOnlySpacesAndTabs()
    {
        var events = Trace.Parse(
            "trace.log",
            "\t # comment\r\n \t\r\nT\tacquire\tA\r\n\t# another comment\nT release A");

        Assert.Equal(
            new[]
            {
                new TraceEvent(3, "T", "acquire", "A"),
                new TraceEvent(5, "T", "release", "A"),
            },
            events);
    }

    [Fact]
    public void SkipsOneLeadingBomAndParsesCrLfLines()
    {
        var events = Trace.Parse(
            "trace.log",
            "\uFEFFT acquire A\r\nT release A\r\n");

        Assert.Equal(
            new[]
            {
                new TraceEvent(1, "T", "acquire", "A"),
                new TraceEvent(2, "T", "release", "A"),
            },
            events);
    }

    [Fact]
    public void DoesNotSkipABomThatIsNotAtTheStart()
    {
        AssertLineError(
            "T acquire A\n\uFEFFT release A",
            2,
            "invalid thread name \"?T\"");
    }

    [Fact]
    public void ParsesAnUnterminatedFinalLine()
    {
        var events = Trace.Parse("trace.log", "T acquire A\n# trailing comment");

        Assert.Equal(new TraceEvent(1, "T", "acquire", "A"), Assert.Single(events));
    }

    [Theory]
    [InlineData("T acquire A\r")]
    [InlineData("T acquire A\r\r\n")]
    public void RemovesOnlyTheCrImmediatelyBeforeLf(string text)
    {
        AssertLineError(text, 1, "invalid lock name \"A?\"");
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("# comment")]
    [InlineData(" \t # comment\r\n\t\r\n")]
    public void RejectsInputWithoutEvents(string text)
    {
        var exception = Assert.Throws<LockorderException>(() => Trace.Parse("empty.log", text));

        Assert.Equal("empty.log: no events", exception.Message);
    }

    [Fact]
    public void DoesNotSkipMalformedEventsForLocksThatMayLaterBeIgnored()
    {
        AssertLineError(
            "T acquire ignored\nT lock ignored",
            2,
            "invalid verb \"lock\" (expected acquire or release)");
    }

    [Fact]
    public void QuotesNonAsciiAsOneQuestionMarkPerCodePoint()
    {
        AssertLineError(
            "T acquire é🙂",
            1,
            "invalid lock name \"??\"");
    }

    [Fact]
    public void AcceptsOneAndFortyCharacterNames()
    {
        const string fortyCharacters = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var events = Trace.Parse(
            "trace.log",
            $"a acquire L\nT acquire {fortyCharacters}");

        Assert.Equal(2, events.Count);
        Assert.Equal("a", events[0].Thread);
        Assert.Equal(fortyCharacters, events[1].Lock);
    }

    [Fact]
    public void RejectsNamesLongerThanFortyCharacters()
    {
        const string fortyOneCharacters = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        AssertLineError(
            $"T acquire {fortyOneCharacters}",
            1,
            $"invalid lock name \"{fortyOneCharacters}\"");
    }

    private static void AssertLineError(string text, int line, string message)
    {
        var exception = Assert.Throws<LockorderException>(() => Trace.Parse("trace.log", text));

        Assert.Equal($"trace.log:{line}: {message}", exception.Message);
    }
}
