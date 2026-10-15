using System.Text;

namespace Lockorder.Core;

public static class Names
{
    public static bool IsValidName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length is < 1 or > 40 || value is "acquire" or "release")
        {
            return false;
        }

        if (!IsAsciiLetter(value[0]) && value[0] != '_')
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!IsAsciiLetter(character)
                && character is not (>= '0' and <= '9')
                && character is not ('_' or '.' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    public static string Show(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var result = new StringBuilder(value.Length + 2);
        result.Append('"');

        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value is >= '!' and <= '~')
            {
                result.Append(rune.ToString());
            }
            else
            {
                result.Append('?');
            }
        }

        result.Append('"');
        return result.ToString();
    }

    private static bool IsAsciiLetter(char character)
    {
        return character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z');
    }
}
