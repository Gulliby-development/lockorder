using Lockorder;
using Lockorder.Core;

namespace Lockorder.Tests;

public class IgnoreListTests
{
    private const string TraceLog =
        "w1 acquire db\n" +
        "w1 acquire cache\n" +
        "w1 release cache\n" +
        "w1 release db\n" +
        "w2 acquire cache\n" +
        "w2 acquire db\n" +
        "w2 release db\n" +
        "w2 release cache\n" +
        "jan acquire log\n" +
        "jan acquire db\n" +
        "jan release db\n" +
        "jan release log\n";

    [Fact]
    public void ACommaListIsTheSameAsRepeatedIgnoreOptions()
    {
        Options list = Options.Parse(["check", "--ignore=cache,log", "trace.log"]);
        Options repeated = Options.Parse(["check", "--ignore", "cache", "--ignore", "log", "trace.log"]);

        Assert.Equal(
            new[] { "cache", "log" },
            list.IgnoredLocks.OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal(
            list.IgnoredLocks.OrderBy(value => value, StringComparer.Ordinal),
            repeated.IgnoredLocks.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void ListsMixWithSingleValuesAndDuplicates()
    {
        Options options = Options.Parse(["edges", "--ignore", "db,cache", "trace.log", "--ignore=cache,log,db", "--ignore", "x"]);

        Assert.Equal(
            new[] { "cache", "db", "log", "x" },
            options.IgnoredLocks.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void AValueWithoutACommaBehavesAsBefore()
    {
        Options options = Options.Parse(["check", "--ignore", "db.pool-1", "trace.log"]);

        Assert.Equal(new[] { "db.pool-1" }, options.IgnoredLocks);
    }

    [Theory]
    [InlineData("db,", "")]
    [InlineData(",db", "")]
    [InlineData(",", "")]
    [InlineData("a,,b", "")]
    [InlineData("a, b", "?b")]
    [InlineData("a,1b", "1b")]
    [InlineData("a,release,b", "release")]
    [InlineData("a,bad/name,1b", "bad/name")]
    [InlineData("a,aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void TheFirstInvalidPartIsReportedAloneWithItsOwnText(string value, string shown)
    {
        LockorderException exception = Assert.Throws<LockorderException>(
            () => Options.Parse(["check", $"--ignore={value}", "trace.log"]));

        Assert.Equal($"invalid --ignore \"{shown}\" (expected a lock name)", exception.Message);
    }

    [Fact]
    public void ValuesAreCheckedInCommandLineOrderAndBeforeTheTraceArgument()
    {
        LockorderException exception = Assert.Throws<LockorderException>(
            () => Options.Parse(["check", "--ignore", "db,1x", "--ignore", ",", "one.log", "two.log"]));

        Assert.Equal("invalid --ignore \"1x\" (expected a lock name)", exception.Message);
    }

    [Theory]
    [InlineData("order", "db\n", 0)]
    [InlineData("threads", "THREAD  ACQUIRES  LOCKS  MAXHELD\njan            1      1        1\nw1             1      1        1\nw2             1      1        1\n", 0)]
    [InlineData("check", "<stdin>: ok, 1 lock, 3 threads\n", 0)]
    public void CommandsReplayWithEveryListedLockIgnored(string command, string expected, int exitCode)
    {
        using var stdin = new StringReader(TraceLog);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int code = Cli.Run([command, "--ignore=cache,log", "-"], stdin, stdout, stderr);

        Assert.Equal(exitCode, code);
        Assert.Equal(expected, stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public void AnInvalidPartIsAUsageErrorWithExitCodeTwo()
    {
        using var stdin = new StringReader(TraceLog);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int code = Cli.Run(["order", "--ignore=cache,,log", "-"], stdin, stdout, stderr);

        Assert.Equal(2, code);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Equal("lockorder: invalid --ignore \"\" (expected a lock name)\n", stderr.ToString());
    }
}
