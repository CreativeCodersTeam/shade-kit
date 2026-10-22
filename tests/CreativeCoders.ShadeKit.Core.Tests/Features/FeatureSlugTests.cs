using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public class FeatureSlugTests
{
    [Theory]
    // Simple titles: lower case, words joined by one hyphen.
    [InlineData("Login SSO", "login-sso")]
    [InlineData("login", "login")]
    [InlineData("Release 2.0", "release-2-0")]
    [InlineData("ABC Def", "abc-def")]
    // Diacritics: the marks are removed, the base letters stay.
    [InlineData("Äpfel und Bäume, café", "apfel-und-baume-cafe")]
    [InlineData("Crème brûlée", "creme-brulee")]
    [InlineData("Ünïcödé", "unicode")]
    [InlineData("Groß", "gro")]
    // Runs of other characters become one hyphen.
    [InlineData("Fix: login -- (SSO)!!", "fix-login-sso")]
    [InlineData("a_b.c/d\\e", "a-b-c-d-e")]
    [InlineData("tab\tand\nnewline", "tab-and-newline")]
    [InlineData("a 日本 b", "a-b")]
    // Leading and trailing separators are trimmed.
    [InlineData("---abc---", "abc")]
    [InlineData("  (Login) ", "login")]
    [InlineData("!a!", "a")]
    public void Create_Title_ReturnsSlug(string title, string expectedSlug)
    {
        // Act
        var slug = FeatureSlug.Create(title);

        // Assert
        slug
            .Should().Be(expectedSlug);
    }

    [Fact]
    public void Create_SlugLongerThanLimit_CutsAtLastHyphenWithinLimit()
    {
        // Arrange
        const string title = "Implement the single sign on login flow for enterprise customers";

        // Act
        var slug = FeatureSlug.Create(title);

        // Assert
        slug
            .Should().Be("implement-the-single-sign-on-login-flow");
    }

    [Fact]
    public void Create_HyphenDirectlyAfterLimit_KeepsFullLimit()
    {
        // Arrange
        var title = $"{new string('a', 19)} {new string('b', 20)} c";

        // Act
        var slug = FeatureSlug.Create(title);

        // Assert
        slug
            .Should().Be($"{new string('a', 19)}-{new string('b', 20)}")
            .And.HaveLength(FeatureSlug.MaxLength);
    }

    [Fact]
    public void Create_SlugOfExactlyLimit_IsNotCut()
    {
        // Arrange
        var title = $"{new string('a', 20)} {new string('b', 19)}";

        // Act
        var slug = FeatureSlug.Create(title);

        // Assert
        slug
            .Should().Be($"{new string('a', 20)}-{new string('b', 19)}")
            .And.HaveLength(FeatureSlug.MaxLength);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(41)]
    public void Create_LongWordWithoutHyphenWithinLimit_HardCutsAtLimit(int wordLength)
    {
        // Arrange
        var title = $"{new string('a', wordLength)} b";

        // Act
        var slug = FeatureSlug.Create(title);

        // Assert
        slug
            .Should().Be(new string('a', FeatureSlug.MaxLength));
    }

    [Fact]
    public void Create_OnlyHyphenWithinLimitAfterFirstCharacter_CutsAtThatHyphen()
    {
        // Arrange
        var title = $"a {new string('b', 50)}";

        // Act
        var slug = FeatureSlug.Create(title);

        // Assert
        slug
            .Should().Be("a");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ???")]
    [InlineData("日本語")]
    [InlineData("ß")]
    public void Create_TitleWithoutAsciiLetterOrDigit_ReturnsEmptySlug(string title)
    {
        // Act
        var slug = FeatureSlug.Create(title);

        // Assert
        slug
            .Should().BeEmpty();
    }

    [Fact]
    public void Create_NullTitle_ThrowsArgumentNullException()
    {
        // Act
        var act = () => FeatureSlug.Create(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("title");
    }
}
