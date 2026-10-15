using System.Runtime.CompilerServices;
using System.Text;
using Lockorder;

namespace Lockorder.Tests;

public class CliTests
{
    [Fact]
    public void HelpWritesExactlyTheFourUsageLines()
    {
        Result result = Run(["--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            "usage: lockorder check [--ignore LOCK]... TRACE\n"
                + "       lockorder edges [--ignore LOCK]... TRACE\n"
                + "       lockorder order [--ignore LOCK]... TRACE\n"
                + "       lockorder threads [--ignore LOCK]... TRACE\n",
            result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Theory]
    [InlineData("", "missing command (check, edges, order or threads)")]
    [InlineData("inspect --ignore", "unknown command \"inspect\"")]
    [InlineData("check --unknown --ignore", "unknown option \"--unknown\"")]
    [InlineData("check --ignore", "--ignore needs a value")]
    [InlineData("check --ignore=release", "invalid --ignore \"release\" (expected a lock name)")]
    [InlineData("check --help", "unknown option \"--help\"")]
    [InlineData("check --ignore=cache", "missing TRACE")]
    [InlineData("check first.log second.log", "too many arguments")]
    public void UsageErrorsWriteOnlyTheExactPrefixedError(string arguments, string message)
    {
        string[] args = arguments.Length == 0 ? [] : arguments.Split(' ');

        Result result = Run(args);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Equal($"lockorder: {message}\n", result.Stderr);
    }

    [Theory]
    [InlineData("é", "?")]
    [InlineData("a\uD83D\uDE80b", "a?b")]
    [InlineData("a\tb", "a?b")]
    public void UsageErrorsDisplayOneQuestionMarkPerNonPrintableCodePoint(string value, string shown)
    {
        Result result = Run(["check", $"--ignore={value}", "trace.log"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Equal(
            $"lockorder: invalid --ignore \"{shown}\" (expected a lock name)\n",
            result.Stderr);
    }

    [Fact]
    public void StdinIsNamedStdinAndUsesTheSameReports()
    {
        Result result = Run(
            ["check", "-"],
            "worker acquire db\nworker release db\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("<stdin>: ok, 1 lock, 1 thread\n", result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public void StdinParsingErrorsUseTheStdinDisplayName()
    {
        Result result = Run(["check", "-"], "worker acquire\n");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Equal(
            "lockorder: <stdin>:1: expected \"THREAD acquire|release LOCK\"\n",
            result.Stderr);
    }

    [Fact]
    public void BomAndCrLfFilesAreParsedWithoutChangingTheInput()
    {
        InTemporaryDirectory(directory =>
        {
            string path = Path.Combine(directory, "trace.log");
            byte[] preamble = new UTF8Encoding(true).GetPreamble();
            byte[] trace = Encoding.UTF8.GetBytes("# header\r\nworker acquire db\r\nworker release db\r\n");
            byte[] contents = [.. preamble, .. trace];
            File.WriteAllBytes(path, contents);

            Result result = Run(["check", path]);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal($"{Display(path)}: ok, 1 lock, 1 thread\n", result.Stdout);
            Assert.Equal(string.Empty, result.Stderr);
            Assert.Equal(contents, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void LateTraceErrorsDoNotEmitPartialReports()
    {
        InTemporaryDirectory(directory =>
        {
            string path = WriteTrace(directory, "worker acquire db\nworker lock cache\n");

            Result result = Run(["check", path]);

            Assert.Equal(2, result.ExitCode);
            Assert.Equal(string.Empty, result.Stdout);
            Assert.Equal(
                $"lockorder: {Display(path)}:2: invalid verb \"lock\" (expected acquire or release)\n",
                result.Stderr);
        });
    }

    [Fact]
    public void MissingTraceFileProducesOnlyTheNormalizedReadError()
    {
        InTemporaryDirectory(directory =>
        {
            string path = Path.Combine(directory, "missing", "trace.log");

            Result result = Run(["edges", path]);

            Assert.Equal(2, result.ExitCode);
            Assert.Equal(string.Empty, result.Stdout);
            Assert.Equal($"lockorder: {Display(path)}: cannot read the file\n", result.Stderr);
        });
    }

    [Fact]
    public void InvalidUtf8FileProducesTheStrictDecoderError()
    {
        InTemporaryDirectory(directory =>
        {
            string path = Path.Combine(directory, "bad-utf8.log");
            File.WriteAllBytes(path, [0x74, 0x31, 0x20, 0xFF, 0x20, 0x61]);

            Result result = Run(["threads", path]);

            Assert.Equal(2, result.ExitCode);
            Assert.Equal(string.Empty, result.Stdout);
            Assert.Equal($"lockorder: {Display(path)}: input is not valid UTF-8\n", result.Stderr);
        });
    }

    [Theory]
    [InlineData("   \n\t# comment\n# another\n", "no events")]
    [InlineData("t1 acquire a b\n", "expected \"THREAD acquire|release LOCK\"")]
    [InlineData("t1 lock a\n", "invalid verb \"lock\" (expected acquire or release)")]
    [InlineData("1t acquire a\n", "invalid thread name \"1t\"")]
    [InlineData("t1 acquire 1a\n", "invalid lock name \"1a\"")]
    public void TraceInputErrorsHaveExactLineOrNoEventsMessages(string text, string message)
    {
        InTemporaryDirectory(directory =>
        {
            string path = WriteTrace(directory, text);

            Result result = Run(["check", path]);

            Assert.Equal(2, result.ExitCode);
            Assert.Equal(string.Empty, result.Stdout);
            string prefix = message == "no events" ? $"{Display(path)}: " : $"{Display(path)}:1: ";
            Assert.Equal($"lockorder: {prefix}{message}\n", result.Stderr);
        });
    }

    [Fact]
    public void IgnoredLocksAreFilteredAfterTheEntireTraceHasBeenParsed()
    {
        Result result = Run(
            ["check", "--ignore", "cache", "-"],
            "worker acquire cache\nworker lock cache\n");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Equal(
            "lockorder: <stdin>:2: invalid verb \"lock\" (expected acquire or release)\n",
            result.Stderr);
    }

    [Fact]
    public void IgnoringEveryEventLeavesZeroLocksAndThreadsRatherThanNoEvents()
    {
        Result result = Run(
            ["check", "--ignore=cache", "-"],
            "worker acquire cache\nworker release cache\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("<stdin>: ok, 0 locks, 0 threads\n", result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public void TraceLogCheckOutputMatchesTheWorkedExampleExactly()
    {
        InTemporaryDirectory(directory =>
        {
            string path = WriteTrace(directory, WorkedTrace);
            string display = Display(path);

            Result result = Run(["check", path]);

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(
                $"{display}:3: LK201 lock order inversion among cache, db (threads w1, w2, w3)\n"
                    + "    cache -> db (first at line 7 by w2)\n"
                    + "    db -> cache (first at line 3 by w1)\n",
                result.Stdout);
            Assert.Equal(string.Empty, result.Stderr);
        });
    }

    [Fact]
    public void TraceLogEdgesOutputMatchesTheWorkedExampleExactly()
    {
        InTemporaryDirectory(directory =>
        {
            string path = WriteTrace(directory, WorkedTrace);

            Result result = Run(["edges", path]);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(
                "FROM   TO     THREADS  LINE\n"
                    + "cache  db           1     7\n"
                    + "db     cache        2     3\n"
                    + "log    db           1    15\n",
                result.Stdout);
            Assert.Equal(string.Empty, result.Stderr);
        });
    }

    [Fact]
    public void TraceLogThreadsOutputMatchesTheWorkedExampleExactly()
    {
        InTemporaryDirectory(directory =>
        {
            string path = WriteTrace(directory, WorkedTrace);

            Result result = Run(["threads", path]);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(
                "THREAD  ACQUIRES  LOCKS  MAXHELD\n"
                    + "jan            2      2        2\n"
                    + "w1             2      2        2\n"
                    + "w2             2      2        2\n"
                    + "w3             2      2        2\n",
                result.Stdout);
            Assert.Equal(string.Empty, result.Stderr);
        });
    }

    [Fact]
    public void TraceLogOrderCycleAndIgnoredOrderMatchTheWorkedExamplesExactly()
    {
        InTemporaryDirectory(directory =>
        {
            string path = WriteTrace(directory, WorkedTrace);
            string display = Display(path);

            Result cyclic = Run(["order", path]);

            Assert.Equal(1, cyclic.ExitCode);
            Assert.Equal(string.Empty, cyclic.Stdout);
            Assert.Equal(
                $"lockorder: {display}: no lock order exists (cycle among cache, db)\n",
                cyclic.Stderr);

            Result ignored = Run(["order", "--ignore", "cache", path]);

            Assert.Equal(0, ignored.ExitCode);
            Assert.Equal("log\ndb\n", ignored.Stdout);
            Assert.Equal(string.Empty, ignored.Stderr);

            Result checkIgnored = Run(["check", "--ignore=cache", path]);

            Assert.Equal(0, checkIgnored.ExitCode);
            Assert.Equal($"{display}: ok, 2 locks, 4 threads\n", checkIgnored.Stdout);
            Assert.Equal(string.Empty, checkIgnored.Stderr);
        });
    }

    [Fact]
    public void BadLogCheckOutputMatchesEveryReplayFindingExactly()
    {
        const string badTrace =
            "t1 acquire a\n"
            + "t1 acquire a\n"
            + "t2 acquire a\n"
            + "t2 release b\n"
            + "t1 release a\n"
            + "t3 acquire c\n";

        Result result = Run(["check", "-"], badTrace);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(
            "<stdin>:2: LK101 t1 acquires a again while holding it\n"
                + "<stdin>:3: LK102 t2 acquires a while t1 holds it\n"
                + "<stdin>:4: LK103 t2 releases b without holding it\n"
                + "<stdin>:6: LK104 t3 still holds c at the end of the trace\n",
            result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public void CodeLikeThreadNamesRemainNamesAndFindingsKeepTheirCodesFirst()
    {
        Result result = Run(
            ["check", "-"],
            "LK103 acquire a\nLK103 release b\n");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(
            "<stdin>:1: LK104 LK103 still holds a at the end of the trace\n"
                + "<stdin>:2: LK103 LK103 releases b without holding it\n",
            result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public void CheckUsesSingularCountsForOneLockAndOneThread()
    {
        Result result = Run(["check", "-"], "w acquire lock\nw release lock\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("<stdin>: ok, 1 lock, 1 thread\n", result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public void EdgesAndThreadsDoNotPrintReplayProblems()
    {
        const string trace =
            "t1 acquire a\n"
            + "t1 acquire a\n"
            + "t2 release b\n"
            + "t1 release a\n";

        Result edges = Run(["edges", "-"], trace);

        Assert.Equal(0, edges.ExitCode);
        Assert.Equal("FROM  TO  THREADS  LINE\n", edges.Stdout);
        Assert.Equal(string.Empty, edges.Stderr);

        Result threads = Run(["threads", "-"], trace);

        Assert.Equal(0, threads.ExitCode);
        Assert.Equal(
            "THREAD  ACQUIRES  LOCKS  MAXHELD\n"
                + "t1             1      1        1\n"
                + "t2             0      0        0\n",
            threads.Stdout);
        Assert.Equal(string.Empty, threads.Stderr);
    }

    [Fact]
    public void OrderUsesCodePointTiesAndIncludesLocksWithoutEdges()
    {
        Result result = Run(
            ["order", "-"],
            "t acquire b\nt acquire d\nt release d\nt release b\n"
                + "t acquire a\nt acquire c\nt release c\nt release a\n"
                + "t acquire x\nt release x\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("a\nb\nc\nd\nx\n", result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public void OrderCycleErrorIncludesTheSortedUnionOfMultipleComponents()
    {
        Result result = Run(
            ["order", "-"],
            "t acquire a\nt acquire b\nt release b\nt release a\n"
                + "t acquire b\nt acquire a\nt release a\nt release b\n"
                + "t acquire y\nt acquire z\nt release z\nt release y\n"
                + "t acquire z\nt acquire y\n");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Equal(
            "lockorder: <stdin>: no lock order exists (cycle among a, b, y, z)\n",
            result.Stderr);
    }

    [Fact]
    public void OrderReportsCyclesEvenWhenOneThreadRecordedEveryEdge()
    {
        Result result = Run(
            ["order", "-"],
            "t acquire a\nt acquire b\nt release b\nt release a\n"
                + "t acquire b\nt acquire a\n");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Equal(
            "lockorder: <stdin>: no lock order exists (cycle among a, b)\n",
            result.Stderr);
    }

    [Fact]
    public void CliWritesLfEvenWhenInjectedWritersUseCrLf()
    {
        using var stdin = new StringReader(string.Empty);
        using var stdout = new StringWriter { NewLine = "\r\n" };
        using var stderr = new StringWriter { NewLine = "\r\n" };

        int exitCode = Cli.Run(["--help"], stdin, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            "usage: lockorder check [--ignore LOCK]... TRACE\n"
                + "       lockorder edges [--ignore LOCK]... TRACE\n"
                + "       lockorder order [--ignore LOCK]... TRACE\n"
                + "       lockorder threads [--ignore LOCK]... TRACE\n",
            stdout.ToString());
        Assert.DoesNotContain("\r", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    private static Result Run(string[] args, string stdinText = "")
    {
        using var stdin = new StringReader(stdinText);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Cli.Run(args, stdin, stdout, stderr);
        return new Result(exitCode, stdout.ToString(), stderr.ToString());
    }

    private static string WriteTrace(string directory, string content)
    {
        string path = Path.Combine(directory, "trace.log");
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    private static string Display(string path)
    {
        return path.Replace('\\', '/');
    }

    private static void InTemporaryDirectory(
        Action<string> action,
        [CallerMemberName] string testName = "")
    {
        string directory = Path.Combine(Path.GetTempPath(), $"{testName}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            action(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed record Result(int ExitCode, string Stdout, string Stderr);

    private const string WorkedTrace =
        "# two workers, one that repeats the first order, and a janitor\n"
        + "w1 acquire db\n"
        + "w1 acquire cache\n"
        + "w1 release cache\n"
        + "w1 release db\n"
        + "w2 acquire cache\n"
        + "w2 acquire db\n"
        + "w2 release db\n"
        + "w2 release cache\n"
        + "w3 acquire db\n"
        + "w3 acquire cache\n"
        + "w3 release cache\n"
        + "w3 release db\n"
        + "jan acquire log\n"
        + "jan acquire db\n"
        + "jan release db\n"
        + "jan release log\n";
}
