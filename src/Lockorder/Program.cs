using System.Text;

namespace Lockorder;

public static class Program
{
    public static int Main(string[] args)
    {
        using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))
        {
            NewLine = "\n",
        };
        using var stderr = new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false))
        {
            NewLine = "\n",
        };

        var exitCode = Cli.Run(args, Console.In, stdout, stderr);
        stdout.Flush();
        stderr.Flush();
        return exitCode;
    }
}
