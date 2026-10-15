using Lockorder;
using Lockorder.Core;

namespace Lockorder.Tests;

public class OptionsTests
{
    [Fact]
    public void ParsesHelpWhenItIsTheOnlyArgument()
    {
        Options options = Options.Parse(["--help"]);

        Assert.True(options.ShowHelp);
        Assert.Equal(string.Empty, options.Command);
        Assert.Equal(string.Empty, options.Trace);
        Assert.Empty(options.IgnoredLocks);
    }

    [Fact]
    public void HelpAfterACommandIsAnUnknownOption()
    {
        LockorderException exception = ParseError("check", "--help", "trace.log");

        Assert.Equal("unknown option \"--help\"", exception.Message);
    }

    [Theory]
    [InlineData("check")]
    [InlineData("edges")]
    [InlineData("order")]
    [InlineData("threads")]
    public void ParsesEachSupportedCommand(string command)
    {
        Options options = Options.Parse([command, "trace.log"]);

        Assert.Equal(command, options.Command);
        Assert.Equal("trace.log", options.Trace);
        Assert.False(options.ShowHelp);
        Assert.Empty(options.IgnoredLocks);
    }

    [Fact]
    public void ParsesDashAsTheStandardInputTrace()
    {
        Options options = Options.Parse(["check", "-"]);

        Assert.Equal("-", options.Trace);
    }

    [Fact]
    public void ParsesRepeatedSeparatedAndEqualsIgnoreOptions()
    {
        Options options = Options.Parse([
            "check",
            "--ignore", "db",
            "trace.log",
            "--ignore=cache",
            "--ignore", "db",
            "--ignore=cache"
        ]);

        Assert.Equal("trace.log", options.Trace);
        Assert.Equal(
            new[] { "cache", "db" },
            options.IgnoredLocks.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void ParsesIgnoreOptionBeforeAndAfterTrace()
    {
        Options options = Options.Parse(["edges", "--ignore=cache", "trace.log", "--ignore", "db"]);

        Assert.Equal("trace.log", options.Trace);
        Assert.Equal(
            new[] { "cache", "db" },
            options.IgnoredLocks.OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void SeparatedIgnoreConsumesAnOptionLookingValue()
    {
        LockorderException exception = ParseError("check", "--ignore", "--not-an-option", "trace.log");

        Assert.Equal("invalid --ignore \"--not-an-option\" (expected a lock name)", exception.Message);
    }

    [Fact]
    public void SeparatedIgnoreConsumesDashAsItsValue()
    {
        LockorderException exception = ParseError("check", "--ignore", "-", "trace.log");

        Assert.Equal("invalid --ignore \"-\" (expected a lock name)", exception.Message);
    }

    [Fact]
    public void EmptyEqualsIgnoreValueIsInvalid()
    {
        LockorderException exception = ParseError("check", "--ignore=", "trace.log");

        Assert.Equal("invalid --ignore \"\" (expected a lock name)", exception.Message);
    }

    [Fact]
    public void TrailingIgnoreWithoutAValueHasItsSpecificError()
    {
        LockorderException exception = ParseError("check", "trace.log", "--ignore");

        Assert.Equal("--ignore needs a value", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("acquire")]
    [InlineData("release")]
    [InlineData("1lock")]
    [InlineData(".lock")]
    [InlineData("-lock")]
    [InlineData("lock/name")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void RejectsInvalidIgnoreNames(string value)
    {
        LockorderException exception = ParseError("check", $"--ignore={value}", "trace.log");

        Assert.Equal(
            $"invalid --ignore \"{value}\" (expected a lock name)",
            exception.Message);
    }

    [Fact]
    public void AcceptsAValidFortyCharacterIgnoreName()
    {
        string lockName = new('a', 40);

        Options options = Options.Parse(["check", "--ignore", lockName, "trace.log"]);

        Assert.Contains(lockName, options.IgnoredLocks);
    }

    [Fact]
    public void ReportsMissingCommandForNoArguments()
    {
        LockorderException exception = ParseError();

        Assert.Equal("missing command (check, edges, order or threads)", exception.Message);
    }

    [Fact]
    public void ReportsUnknownCommandBeforeScanningTheRemainingArguments()
    {
        LockorderException exception = ParseError("inspect", "--ignore");

        Assert.Equal("unknown command \"inspect\"", exception.Message);
    }

    [Fact]
    public void ReportsTheFirstOptionErrorEncounteredFromLeftToRight()
    {
        LockorderException exception = ParseError("check", "--unknown", "--ignore");

        Assert.Equal("unknown option \"--unknown\"", exception.Message);
    }

    [Fact]
    public void InvalidIgnoreIsReportedBeforeMissingTraceAndExcessTrace()
    {
        LockorderException exception = ParseError("check", "--ignore", "release", "one.log", "two.log");

        Assert.Equal("invalid --ignore \"release\" (expected a lock name)", exception.Message);
    }

    [Fact]
    public void ReportsMissingTraceAfterValidOptions()
    {
        LockorderException exception = ParseError("check", "--ignore=cache");

        Assert.Equal("missing TRACE", exception.Message);
    }

    [Fact]
    public void ReportsTooManyTraceArgumentsAfterValidOptions()
    {
        LockorderException exception = ParseError("check", "--ignore=cache", "one.log", "two.log");

        Assert.Equal("too many arguments", exception.Message);
    }

    [Fact]
    public void CliHelpPrintsExactlyFourUsageLinesAndExitsSuccessfully()
    {
        using var stdin = new StringReader(string.Empty);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Cli.Run(["--help"], stdin, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            "usage: lockorder check [--ignore LOCK]... TRACE\n"
                + "       lockorder edges [--ignore LOCK]... TRACE\n"
                + "       lockorder order [--ignore LOCK]... TRACE\n"
                + "       lockorder threads [--ignore LOCK]... TRACE\n",
            stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public void CliUsageErrorWritesOnlyThePrefixedMessageToStandardError()
    {
        using var stdin = new StringReader(string.Empty);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Cli.Run(["check", "--help"], stdin, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Equal("lockorder: unknown option \"--help\"\n", stderr.ToString());
    }

    private static LockorderException ParseError(params string[] args)
    {
        return Assert.Throws<LockorderException>(() => Options.Parse(args));
    }
}
