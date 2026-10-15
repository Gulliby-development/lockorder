using Lockorder.Core;
using Xunit;

namespace Lockorder.Tests;

public class TableTests
{
    [Fact]
    public void HeaderOnlyTablePrintsTheHeaderAndFinalLineFeed()
    {
        string output = Table.Render(
            ["FROM", "TO"],
            []);

        Assert.Equal("FROM  TO\n", output);
    }

    [Fact]
    public void HeaderOnlySingleColumnHasNoSeparatorOrTrailingPadding()
    {
        string output = Table.Render(
            ["LOCK"],
            []);

        Assert.Equal("LOCK\n", output);
    }

    [Fact]
    public void ColumnWidthsUseTheWidestHeaderOrCell()
    {
        string output = Table.Render(
            ["A", "B"],
            [
                ["long", "x"],
                ["q", "wide"],
            ]);

        Assert.Equal("A     B\nlong  x\nq     wide\n", output);
    }

    [Fact]
    public void UnmarkedTextColumnsAreLeftAligned()
    {
        string output = Table.Render(
            ["THREAD", "ACTION"],
            [
                ["w", "acquire"],
                ["worker", "release"],
            ]);

        Assert.Equal("THREAD  ACTION\nw       acquire\nworker  release\n", output);
    }

    [Fact]
    public void RightAlignedColumnPadsItsHeaderAndNumericCellsOnTheLeft()
    {
        string output = Table.Render(
            ["N", "COUNT"],
            [
                ["a", "2"],
                ["b", "12"],
            ],
            [1]);

        Assert.Equal("N  COUNT\na      2\nb     12\n", output);
    }

    [Fact]
    public void RightAlignedColumnKeepsTheWiderHeaderUnpadded()
    {
        string output = Table.Render(
            ["THREAD", "COUNT"],
            [new[] { "t", "3" }],
            [1]);

        Assert.Equal("THREAD  COUNT\nt           3\n", output);
    }

    [Fact]
    public void LastColumnIsNotPaddedToItsWidth()
    {
        string output = Table.Render(
            ["A", "LAST"],
            [
                ["x", "long"],
                ["longer", "y"],
            ]);

        Assert.Equal("A       LAST\nx       long\nlonger  y\n", output);
    }

    [Fact]
    public void OutputUsesLfOnlyAndHasNoTrailingSpaces()
    {
        string output = Table.Render(
            ["FROM", "TO", "COUNT"],
            [
                ["a", "long", "1"],
                ["long", "b", "12"],
            ],
            [2]);

        Assert.DoesNotContain("\r", output);
        Assert.EndsWith("\n", output);
        Assert.All(output.Split('\n').SkipLast(1), line =>
        {
            Assert.NotEmpty(line);
            Assert.False(char.IsWhiteSpace(line[^1]));
        });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void AlignmentColumnMustBeWithinTheHeader(int column)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Table.Render(
                ["A", "B"],
                [],
                [column]));
    }
}
