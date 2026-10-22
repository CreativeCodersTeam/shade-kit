using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

public class MarkdownCodeFencesTests
{
    [Fact]
    public void FindLinesOutsideCodeFences_NoFence_ReturnsEveryLine()
    {
        // Act
        var (lineIndexes, unclosedFenceIndex) =
            MarkdownCodeFences.FindLinesOutsideCodeFences(["# Title", "", "Text"]);

        // Assert
        lineIndexes
            .Should().Equal(0, 1, 2);

        unclosedFenceIndex
            .Should().BeNull();
    }

    [Fact]
    public void FindLinesOutsideCodeFences_EmptyList_ReturnsNoLine()
    {
        // Act
        var (lineIndexes, unclosedFenceIndex) = MarkdownCodeFences.FindLinesOutsideCodeFences([]);

        // Assert
        lineIndexes
            .Should().BeEmpty();

        unclosedFenceIndex
            .Should().BeNull();
    }

    [Theory]
    [InlineData("```", "```")]
    [InlineData("~~~", "~~~")]
    [InlineData("```sh", "```")]
    [InlineData("~~~ info `with` backticks", "~~~")]
    [InlineData("   ```", "```")]
    [InlineData("````", "`````")]
    [InlineData("```", "```  ")]
    [InlineData("```\r", "```\r")]
    public void FindLinesOutsideCodeFences_ClosedFence_ExcludesBlockIncludingFenceLines(string openingFence,
        string closingFence)
    {
        // Act
        var (lineIndexes, unclosedFenceIndex) =
            MarkdownCodeFences.FindLinesOutsideCodeFences(["Before", openingFence, "# Inside", closingFence, "After"]);

        // Assert
        lineIndexes
            .Should().Equal(0, 4);

        unclosedFenceIndex
            .Should().BeNull();
    }

    [Theory]
    [InlineData("```", "~~~")]
    [InlineData("~~~", "```")]
    [InlineData("````", "```")]
    [InlineData("```", "``` sh")]
    public void FindLinesOutsideCodeFences_LineThatDoesNotCloseFence_ReturnsUnclosedFenceIndex(string openingFence,
        string otherFence)
    {
        // Act
        var (lineIndexes, unclosedFenceIndex) =
            MarkdownCodeFences.FindLinesOutsideCodeFences(["Before", openingFence, otherFence, "After"]);

        // Assert
        lineIndexes
            .Should().Equal(0);

        unclosedFenceIndex
            .Should().Be(1);
    }

    [Theory]
    [InlineData("    ```")]
    [InlineData("``")]
    [InlineData("~~")]
    [InlineData("```code```")]
    [InlineData("``` sh `x`")]
    [InlineData("\t```")]
    public void FindLinesOutsideCodeFences_LineThatIsNoFence_ReturnsEveryLine(string line)
    {
        // Act
        var (lineIndexes, unclosedFenceIndex) =
            MarkdownCodeFences.FindLinesOutsideCodeFences(["Before", line, "After"]);

        // Assert
        lineIndexes
            .Should().Equal(0, 1, 2);

        unclosedFenceIndex
            .Should().BeNull();
    }

    [Fact]
    public void FindLinesOutsideCodeFences_SecondFenceUnclosed_ReturnsIndexOfSecondFence()
    {
        // Act
        var (lineIndexes, unclosedFenceIndex) =
            MarkdownCodeFences.FindLinesOutsideCodeFences(["```", "code", "```", "Text", "~~~", "code"]);

        // Assert
        lineIndexes
            .Should().Equal(3);

        unclosedFenceIndex
            .Should().Be(4);
    }
}
