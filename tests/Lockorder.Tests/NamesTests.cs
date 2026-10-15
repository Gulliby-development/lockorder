using Lockorder.Core;

namespace Lockorder.Tests;

public class NamesTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("Z")]
    [InlineData("_")]
    [InlineData("w1")]
    [InlineData("db.pool")]
    [InlineData("T-2")]
    [InlineData("a_b.c-9")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    [InlineData("abcdefghijklmnopqrstuvwxyz")]
    [InlineData("A1234567890123456789012345678901234567")]
    [InlineData("ACQUIRE")]
    [InlineData("Release")]
    public void IsValidNameAcceptsValidAsciiNames(string value)
    {
        Assert.True(Names.IsValidName(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("acquire")]
    [InlineData("release")]
    [InlineData("1t")]
    [InlineData(".name")]
    [InlineData("-name")]
    [InlineData(" name")]
    [InlineData("name ")]
    [InlineData("a b")]
    [InlineData("a\tb")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData("é")]
    [InlineData("aé")]
    [InlineData("名前")]
    [InlineData("😀")]
    public void IsValidNameRejectsInvalidNames(string value)
    {
        Assert.False(Names.IsValidName(value));
    }

    [Fact]
    public void IsValidNameAcceptsTheFortyCharacterLimit()
    {
        Assert.True(Names.IsValidName("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
    }

    [Fact]
    public void IsValidNameRejectsANameOneCharacterOverTheLimit()
    {
        Assert.False(Names.IsValidName("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
    }

    [Fact]
    public void IsValidNameThrowsForNull()
    {
        Assert.Throws<ArgumentNullException>(() => Names.IsValidName(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("letters123")]
    [InlineData("!~")]
    [InlineData("quote\"and\\slash")]
    public void ShowPreservesPrintableAsciiWithoutEscaping(string value)
    {
        Assert.Equal($"\"{value}\"", Names.Show(value));
    }

    [Fact]
    public void ShowReplacesSpacesBecauseTheyAreOutsideThePrintableRange()
    {
        Assert.Equal("\"with?spaces\"", Names.Show("with spaces"));
    }

    [Theory]
    [InlineData("\0", "?")]
    [InlineData("\t", "?")]
    [InlineData("\n", "?")]
    [InlineData("\r", "?")]
    [InlineData("\u007f", "?")]
    [InlineData("\u0085", "?")]
    [InlineData("é", "?")]
    [InlineData("😀", "?")]
    [InlineData("e\u0301", "e?")]
    public void ShowReplacesEachNonPrintableCodePointWithOneQuestionMark(string value, string expectedBody)
    {
        Assert.Equal($"\"{expectedBody}\"", Names.Show(value));
    }

    [Fact]
    public void ShowWrapsTheResultInDoubleQuotes()
    {
        Assert.Equal("\"\"", Names.Show(string.Empty));
    }

    [Fact]
    public void ShowThrowsForNull()
    {
        Assert.Throws<ArgumentNullException>(() => Names.Show(null!));
    }
}
