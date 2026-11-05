using Lockorder;

namespace Lockorder.Tests;

public sealed class EdgesMinThreadsTests
{
    private const string Trace =
        "w1 acquire db\n" +
        "w1 acquire cache\n" +
        "w1 release cache\n" +
        "w1 release db\n" +
        "w2 acquire cache\n" +
        "w2 acquire db\n" +
        "w2 release db\n" +
        "w2 release cache\n" +
        "w3 acquire db\n" +
        "w3 acquire cache\n" +
        "w3 release cache\n" +
        "w3 release db\n" +
        "jan acquire log\n" +
        "jan acquire db\n" +
        "jan release db\n" +
        "jan release log\n";

    private const string AllEdges =
        "FROM   TO     THREADS  LINE\n" +
        "cache  db           1     6\n" +
        "db     cache        2     2\n" +
        "log    db           1    14\n";

    [Fact]
    public void WithoutTheOptionEveryEdgeIsListed()
    {
        (int exitCode, string stdout, string stderr) = Run("edges", "-");

        Assert.Equal(0, exitCode);
        Assert.Equal(AllEdges, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void OneKeepsEveryEdge()
    {
        (int exitCode, string stdout, _) = Run("edges", "--min-threads", "1", "-");

        Assert.Equal(0, exitCode);
        Assert.Equal(AllEdges, stdout);
    }

    [Fact]
    public void TwoKeepsTheEdgesRecordedByAtLeastTwoThreads()
    {
        (int exitCode, string stdout, string stderr) = Run("edges", "--min-threads", "2", "-");

        Assert.Equal(0, exitCode);
        Assert.Equal("FROM  TO     THREADS  LINE\ndb    cache        2     2\n", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void TheEqualsFormIsAccepted()
    {
        (int exitCode, string stdout, _) = Run("edges", "--min-threads=2", "-");

        Assert.Equal(0, exitCode);
        Assert.Equal("FROM  TO     THREADS  LINE\ndb    cache        2     2\n", stdout);
    }

    [Fact]
    public void AFilterThatLeavesNoEdgeStillWritesTheHeaderAndExitsZero()
    {
        (int exitCode, string stdout, string stderr) = Run("edges", "--min-threads", "3", "-");

        Assert.Equal(0, exitCode);
        Assert.Equal("FROM  TO  THREADS  LINE\n", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void TheFilterAppliesAfterIgnoredLocksAreLeftOut()
    {
        (int exitCode, string stdout, _) = Run("edges", "--ignore", "cache", "--min-threads", "1", "-");

        Assert.Equal(0, exitCode);
        Assert.Equal("FROM  TO  THREADS  LINE\nlog   db        1    14\n", stdout);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("01")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("x")]
    [InlineData("")]
    public void AValueOutsideOneToOneHundredIsAUsageError(string value)
    {
        (int exitCode, string stdout, string stderr) = Run("edges", "--min-threads", value, "-");

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Equal($"lockorder: invalid --min-threads \"{value}\" (expected an integer from 1 to 100)\n", stderr);
    }

    [Fact]
    public void AMissingValueIsAUsageError()
    {
        (int exitCode, string stdout, string stderr) = Run("edges", "-", "--min-threads");

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Equal("lockorder: --min-threads needs a value\n", stderr);
    }

    [Theory]
    [InlineData("check")]
    [InlineData("order")]
    [InlineData("threads")]
    public void OtherCommandsRefuseTheOptionAsUnknown(string command)
    {
        (int exitCode, string stdout, string stderr) = Run(command, "--min-threads", "2", "-");

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Equal("lockorder: unknown option \"--min-threads\"\n", stderr);
    }

    [Fact]
    public void TheLastValueWins()
    {
        (int exitCode, string stdout, _) = Run("edges", "--min-threads", "3", "--min-threads=2", "-");

        Assert.Equal(0, exitCode);
        Assert.Equal("FROM  TO     THREADS  LINE\ndb    cache        2     2\n", stdout);
    }

    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
    {
        using var stdin = new StringReader(Trace);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Cli.Run(args, stdin, stdout, stderr);

        return (exitCode, stdout.ToString(), stderr.ToString());
    }
}
