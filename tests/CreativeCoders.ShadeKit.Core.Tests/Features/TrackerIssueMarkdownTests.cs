using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Trackers;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public class TrackerIssueMarkdownTests
{
    private static readonly DateTimeOffset FirstCreatedAt = new(2026, 10, 20, 15, 30, 45, TimeSpan.Zero);

    private static readonly DateTimeOffset SecondCreatedAt = new(2026, 10, 21, 8, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Format_NullIssue_ThrowsArgumentNullException()
    {
        // Act
        var act = () => TrackerIssueMarkdown.Format(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Format_IssueWithBodyAndComments_ReturnsTitleBodyAndCommentsSection()
    {
        // Arrange
        var issue = new TrackerIssue("Login with SSO", "Users sign in with their company account.",
        [
            new TrackerIssueComment("octocat", FirstCreatedAt, "Which identity provider?"),
            new TrackerIssueComment("jane", SecondCreatedAt, "Entra ID first.")
        ]);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be("# Login with SSO\n" +
                         "\n" +
                         "Users sign in with their company account.\n" +
                         "\n" +
                         "## Comments\n" +
                         "\n" +
                         "### octocat · 2026-10-20T15:30:45+00:00\n" +
                         "\n" +
                         "Which identity provider?\n" +
                         "\n" +
                         "### jane · 2026-10-21T08:00:00+02:00\n" +
                         "\n" +
                         "Entra ID first.\n");
    }

    [Fact]
    public void Format_IssueWithoutComments_ReturnsTitleAndBodyWithoutCommentsSection()
    {
        // Arrange
        var issue = new TrackerIssue("Login with SSO", "Users sign in with their company account.", []);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be("# Login with SSO\n\nUsers sign in with their company account.\n");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\n")]
    public void Format_EmptyOrWhitespaceBodyWithoutComments_ReturnsOnlyTitle(string body)
    {
        // Arrange
        var issue = new TrackerIssue("Login with SSO", body, []);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be("# Login with SSO\n");
    }

    [Fact]
    public void Format_EmptyBodyWithComments_LeavesOutBodyParagraph()
    {
        // Arrange
        var issue = new TrackerIssue("Login with SSO", string.Empty,
            [new TrackerIssueComment("octocat", FirstCreatedAt, "Which identity provider?")]);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be("# Login with SSO\n" +
                         "\n" +
                         "## Comments\n" +
                         "\n" +
                         "### octocat · 2026-10-20T15:30:45+00:00\n" +
                         "\n" +
                         "Which identity provider?\n");
    }

    [Fact]
    public void Format_CommentWithEmptyBody_LeavesOutCommentBodyParagraph()
    {
        // Arrange
        var issue = new TrackerIssue("Login with SSO", "Body.",
        [
            new TrackerIssueComment("octocat", FirstCreatedAt, string.Empty),
            new TrackerIssueComment("jane", SecondCreatedAt, "Entra ID first.")
        ]);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be("# Login with SSO\n" +
                         "\n" +
                         "Body.\n" +
                         "\n" +
                         "## Comments\n" +
                         "\n" +
                         "### octocat · 2026-10-20T15:30:45+00:00\n" +
                         "\n" +
                         "### jane · 2026-10-21T08:00:00+02:00\n" +
                         "\n" +
                         "Entra ID first.\n");
    }

    [Fact]
    public void Format_CommentWithWhitespaceOnlyBody_LeavesOutCommentBodyParagraph()
    {
        // Arrange
        var issue = new TrackerIssue("T", string.Empty,
            [new TrackerIssueComment("octocat", FirstCreatedAt, "  \n")]);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be("# T\n\n## Comments\n\n### octocat · 2026-10-20T15:30:45+00:00\n");
    }

    [Fact]
    public void Format_CommentsNotInTimeOrder_KeepsListOrder()
    {
        // Arrange
        var issue = new TrackerIssue("T", string.Empty,
        [
            new TrackerIssueComment("jane", SecondCreatedAt, "Later."),
            new TrackerIssueComment("octocat", FirstCreatedAt, "Earlier.")
        ]);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be("# T\n\n## Comments\n\n### jane · 2026-10-21T08:00:00+02:00\n\nLater.\n\n" +
                         "### octocat · 2026-10-20T15:30:45+00:00\n\nEarlier.\n");
    }

    [Fact]
    public void Format_BodyWithMarkdownAndLineEndings_KeepsBodyUnchangedExceptTrailingLineBreaks()
    {
        // Arrange
        const string body = "## Steps\r\n\r\n1. Open *login*\r\n2. Ümlaut   \n\n```cs\nvar x = 1;\n```\n";
        var issue = new TrackerIssue("Login with SSO", body, []);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be("# Login with SSO\n\n## Steps\r\n\r\n1. Open *login*\r\n2. Ümlaut   \n\n" +
                         "```cs\nvar x = 1;\n```\n");
    }

    [Theory]
    [InlineData("Body.\n")]
    [InlineData("Body.\r\n")]
    [InlineData("Body.\r\n\r\n\n")]
    [InlineData("Body.\r")]
    public void Format_BodyEndingInLineBreaksFollowedByComments_SeparatesBlocksByOneEmptyLine(string body)
    {
        // Arrange
        var issue = new TrackerIssue("T", body,
            [new TrackerIssueComment("octocat", FirstCreatedAt, "Which identity provider?")]);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be("# T\n\nBody.\n\n## Comments\n\n### octocat · 2026-10-20T15:30:45+00:00\n\n" +
                         "Which identity provider?\n");
    }

    [Fact]
    public void Format_CommentBodiesEndingInLineBreaks_SeparatesBlocksByOneEmptyLineAndEndsWithOneLineFeed()
    {
        // Arrange
        var issue = new TrackerIssue("T", string.Empty,
        [
            new TrackerIssueComment("jane", SecondCreatedAt, "Later.\r\n\r\n"),
            new TrackerIssueComment("octocat", FirstCreatedAt, "Earlier.\n\n")
        ]);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be("# T\n\n## Comments\n\n### jane · 2026-10-21T08:00:00+02:00\n\nLater.\n\n" +
                         "### octocat · 2026-10-20T15:30:45+00:00\n\nEarlier.\n");
    }

    [Theory]
    [InlineData("\tLogin SSO\n", "Login SSO")]
    [InlineData("  Login with SSO  ", "Login with SSO")]
    [InlineData("Login\r\nwith SSO", "Login with SSO")]
    [InlineData("Login\t\t\nwith\u0001SSO", "Login with SSO")]
    [InlineData("\u0001\u001FLogin with SSO\u007F", "Login with SSO")]
    [InlineData("Login \t with SSO", "Login   with SSO")]
    public void Format_TitleWithControlCharactersOrSurroundingWhitespace_WritesOneLineHeading(string title,
        string expectedHeadingText)
    {
        // Arrange
        var issue = new TrackerIssue(title, "Body.", []);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be($"# {expectedHeadingText}\n\nBody.\n");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Format_EmptyTitleAfterCleanup_LeavesOutHeadingLine(string title)
    {
        // Arrange
        var issue = new TrackerIssue(title, "Body.",
            [new TrackerIssueComment("octocat", FirstCreatedAt, "Which identity provider?")]);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Be("Body.\n\n## Comments\n\n### octocat · 2026-10-20T15:30:45+00:00\n\n" +
                         "Which identity provider?\n");
    }

    [Fact]
    public void Format_EmptyTitleAndBodyWithoutComments_ReturnsEmptyText()
    {
        // Arrange
        var issue = new TrackerIssue(" \n", "\r\n", []);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().BeEmpty();
    }

    [Fact]
    public void Format_CommentTimeWithNegativeOffset_FormatsTimeWithItsOffset()
    {
        // Arrange
        var issue = new TrackerIssue("T", string.Empty,
        [
            new TrackerIssueComment("ghost", new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5)), "Hi")
        ]);

        // Act
        var markdown = TrackerIssueMarkdown.Format(issue);

        // Assert
        markdown
            .Should().Contain("### ghost · 2026-01-02T03:04:05-05:00\n");
    }
}
