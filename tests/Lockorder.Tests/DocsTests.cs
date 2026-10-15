using System.IO;

namespace Lockorder.Tests;

public sealed class DocsTests
{
    private const string UsageLines =
        "usage: lockorder check [--ignore LOCK]... TRACE\n" +
        "       lockorder edges [--ignore LOCK]... TRACE\n" +
        "       lockorder order [--ignore LOCK]... TRACE\n" +
        "       lockorder threads [--ignore LOCK]... TRACE\n";

    private const string TraceLog =
        "# two workers, one that repeats the first order, and a janitor\n" +
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

    private const string CheckOutput =
        "trace.log:3: LK201 lock order inversion among cache, db (threads w1, w2, w3)\n" +
        "    cache -> db (first at line 7 by w2)\n" +
        "    db -> cache (first at line 3 by w1)\n";

    private const string EdgesOutput =
        "FROM   TO     THREADS  LINE\n" +
        "cache  db           1     7\n" +
        "db     cache        2     3\n" +
        "log    db           1    15\n";

    private const string ThreadsOutput =
        "THREAD  ACQUIRES  LOCKS  MAXHELD\n" +
        "jan            2      2        2\n" +
        "w1             2      2        2\n" +
        "w2             2      2        2\n" +
        "w3             2      2        2\n";

    private const string OrderOutput = "log\ndb\n";

    [Fact]
    public void FindsReadmeByWalkingUpToSolution()
    {
        Assert.True(File.Exists(Path.Combine(FindRepositoryRoot(), "README.md")));
    }

    [Fact]
    public void ContainsRequiredHeadings()
    {
        string readme = ReadReadme();

        Assert.Contains("## Install", readme);
        Assert.Contains("## Usage", readme);
        Assert.Contains("## Trace file", readme);
        Assert.Contains("## Findings", readme);
        Assert.Contains("## Exit codes", readme);
        Assert.Contains("## Development", readme);
    }

    [Fact]
    public void DocumentsAudienceAndInstallCommands()
    {
        string readme = ReadReadme();

        Assert.Contains("developers debug deadlocks", readme);
        Assert.Contains("dotnet build", readme);
        Assert.Contains("dotnet run --project src/Lockorder -- ARGS", readme);
    }

    [Fact]
    public void CopiesTheFourUsageLinesExactly()
    {
        Assert.Contains(NormalizeLineEndings(UsageLines), NormalizeLineEndings(ReadReadme()));
    }

    [Fact]
    public void CopiesTheTraceLogExampleExactly()
    {
        Assert.Contains(NormalizeLineEndings(TraceLog), NormalizeLineEndings(ReadReadme()));
    }

    [Fact]
    public void CopiesTheCheckOutputExactly()
    {
        Assert.Contains(NormalizeLineEndings(CheckOutput), NormalizeLineEndings(ReadReadme()));
    }

    [Fact]
    public void CopiesTheEdgesOutputExactly()
    {
        Assert.Contains(NormalizeLineEndings(EdgesOutput), NormalizeLineEndings(ReadReadme()));
    }

    [Fact]
    public void CopiesTheThreadsOutputExactly()
    {
        Assert.Contains(NormalizeLineEndings(ThreadsOutput), NormalizeLineEndings(ReadReadme()));
    }

    [Fact]
    public void DocumentsTheOrderIgnoreCommandAndOutputExactly()
    {
        string readme = NormalizeLineEndings(ReadReadme());

        Assert.Contains("lockorder order --ignore cache trace.log", readme);
        Assert.Contains(NormalizeLineEndings(OrderOutput), readme);
    }

    [Fact]
    public void DocumentsAllFindingsAndExitCodes()
    {
        string readme = ReadReadme();

        Assert.Contains("LK101", readme);
        Assert.Contains("LK102", readme);
        Assert.Contains("LK103", readme);
        Assert.Contains("LK104", readme);
        Assert.Contains("LK201", readme);
        Assert.Contains("0", readme);
        Assert.Contains("1", readme);
        Assert.Contains("2", readme);
    }

    [Fact]
    public void DocumentsDevelopmentCommands()
    {
        string readme = ReadReadme();

        Assert.Contains("dotnet format --verify-no-changes", readme);
        Assert.Contains("dotnet test", readme);
    }

    private static string ReadReadme()
    {
        string root = FindRepositoryRoot();
        return NormalizeLineEndings(File.ReadAllText(Path.Combine(root, "README.md")));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Lockorder.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Lockorder.slnx above the test base directory.");
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
