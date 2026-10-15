namespace Lockorder.Core;

public sealed record TraceEvent(int Line, string Thread, string Verb, string Lock);

public static class Trace
{
    private const string ExpectedEvent = "expected \"THREAD acquire|release LOCK\"";

    public static IReadOnlyList<TraceEvent> Parse(string display, string text)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        var events = new List<TraceEvent>();
        var lineNumber = 1;
        var start = 0;

        while (start < text.Length)
        {
            var newline = text.IndexOf('\n', start);
            var hasNewline = newline >= 0;
            var end = hasNewline ? newline : text.Length;
            var line = text[start..end];

            if (hasNewline && line.EndsWith('\r'))
            {
                line = line[..^1];
            }

            ParseLine(display, lineNumber, line, events);

            if (!hasNewline)
            {
                break;
            }

            start = newline + 1;
            lineNumber++;
        }

        if (events.Count == 0)
        {
            throw new LockorderException($"{display}: no events");
        }

        return events;
    }

    private static void ParseLine(string display, int lineNumber, string line, ICollection<TraceEvent> events)
    {
        var trimmed = line.Trim(' ', '\t');
        if (trimmed.Length == 0 || trimmed[0] == '#')
        {
            return;
        }

        var tokens = SplitTokens(trimmed);
        if (tokens.Count != 3)
        {
            throw LineError(display, lineNumber, ExpectedEvent);
        }

        var thread = tokens[0];
        var verb = tokens[1];
        var @lock = tokens[2];

        if (verb is not ("acquire" or "release"))
        {
            throw LineError(
                display,
                lineNumber,
                $"invalid verb {Names.Show(verb)} (expected acquire or release)");
        }

        if (!Names.IsValidName(thread))
        {
            throw LineError(display, lineNumber, $"invalid thread name {Names.Show(thread)}");
        }

        if (!Names.IsValidName(@lock))
        {
            throw LineError(display, lineNumber, $"invalid lock name {Names.Show(@lock)}");
        }

        events.Add(new TraceEvent(lineNumber, thread, verb, @lock));
    }

    private static List<string> SplitTokens(string line)
    {
        var tokens = new List<string>();
        var index = 0;

        while (index < line.Length)
        {
            while (index < line.Length && IsSeparator(line[index]))
            {
                index++;
            }

            if (index == line.Length)
            {
                break;
            }

            var start = index;
            while (index < line.Length && !IsSeparator(line[index]))
            {
                index++;
            }

            tokens.Add(line[start..index]);
        }

        return tokens;
    }

    private static bool IsSeparator(char character)
    {
        return character is ' ' or '\t';
    }

    private static LockorderException LineError(string display, int lineNumber, string message)
    {
        return new LockorderException($"{display}:{lineNumber}: {message}");
    }
}
