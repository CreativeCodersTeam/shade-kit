using System.Globalization;
using System.Text.Json;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Trackers;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Trackers;

// These tests never start the real gh: the runner is faked and returns JSON fixtures.
public class GitHubCliIssueReaderTests
{
    private const string IssueUrl = "https://github.com/octo/repo/issues/42";

    private const string WorkingDirectory = "/home/jane/repo";

    private const string FullIssueJson =
        """
        {
          "title": "Login with SSO",
          "body": "Users sign in with their company account.\r\n\r\n- Ümlaut stays",
          "comments": [
            {
              "author": { "login": "octocat" },
              "authorAssociation": "MEMBER",
              "body": "Which identity provider?",
              "createdAt": "2026-10-20T15:30:45Z",
              "id": "IC_1"
            },
            {
              "author": { "login": "jane" },
              "body": "Entra ID first.",
              "createdAt": "2026-10-21T08:00:00+02:00"
            }
          ]
        }
        """;

    private readonly IGhCommandRunner _ghCommandRunner = A.Fake<IGhCommandRunner>();

    [Fact]
    public void Constructor_NullGhCommandRunner_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GitHubCliIssueReader(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ghCommandRunner");
    }

    [Fact]
    public async Task ReadIssueAsync_NullIssueUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(null!, WorkingDirectory, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("issueUrl");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadIssueAsync_InvalidWorkingDirectory_ThrowsArgumentException(string? workingDirectory)
    {
        // Arrange
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(new Uri(IssueUrl), workingDirectory!,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        AssertRunnerNotCalled();
    }

    [Theory]
    [InlineData("--repo=octo/repo")]
    [InlineData("issues/42")]
    [InlineData("ftp://github.com/octo/repo/issues/42")]
    [InlineData("file:///home/jane/issue.json")]
    public async Task ReadIssueAsync_NotAnAbsoluteHttpUrl_ThrowsTrackerIssueReadFailedExceptionWithoutRunningGh(
        string url)
    {
        // Arrange
        var issueUrl = new Uri(url, UriKind.RelativeOrAbsolute);
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(issueUrl, WorkingDirectory, TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<TrackerIssueReadFailedException>()).Which;

        exception.IssueUrl
            .Should().BeSameAs(issueUrl);

        exception.Reason
            .Should().Be("Only absolute http and https URLs can be read as issues.");

        AssertRunnerNotCalled();
    }

    [Fact]
    public async Task ReadIssueAsync_IssueWithBodyAndComments_ReturnsIssueInGhOrder()
    {
        // Arrange
        SetupGhOutput(FullIssueJson);
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        issue.Title
            .Should().Be("Login with SSO");

        issue.Body
            .Should().Be("Users sign in with their company account.\r\n\r\n- Ümlaut stays");

        issue.Comments
            .Should().Equal(
                new TrackerIssueComment("octocat", new DateTimeOffset(2026, 10, 20, 15, 30, 45, TimeSpan.Zero),
                    "Which identity provider?"),
                new TrackerIssueComment("jane", new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.FromHours(2)),
                    "Entra ID first."));
    }

    [Fact]
    public async Task ReadIssueAsync_CommentTimes_KeepOffsetOfGhOutput()
    {
        // Arrange
        SetupGhOutput(FullIssueJson);
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        issue.Comments.Select(x => x.CreatedAt.Offset)
            .Should().Equal(TimeSpan.Zero, TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task ReadIssueAsync_ValidUrl_RunsGhIssueViewWithJsonFieldsInWorkingDirectory()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        SetupGhOutput(FullIssueJson);
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory, cancellationTokenSource.Token);

        // Assert
        A.CallTo(() => _ghCommandRunner.RunAsync(WorkingDirectory,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs("issue", "view", IssueUrl, "--json",
                    "title,body,comments"),
                cancellationTokenSource.Token))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReadIssueAsync_GitHubEnterpriseUrl_PassesAbsoluteUrlToGh()
    {
        // Arrange
        const string enterpriseUrl = "https://ghe.example.com/team/app/issues/7";
        SetupGhOutput("""{ "title": "T", "body": "", "comments": [] }""");
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        await sut.ReadIssueAsync(new Uri(enterpriseUrl), WorkingDirectory, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.Matches(x => x[2] == enterpriseUrl), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("""{ "title": "Login with SSO", "body": "Text.", "comments": [] }""")]
    [InlineData("""{ "title": "Login with SSO", "body": "Text.", "comments": null }""")]
    [InlineData("""{ "title": "Login with SSO", "body": "Text." }""")]
    public async Task ReadIssueAsync_NoComments_ReturnsIssueWithoutComments(string json)
    {
        // Arrange
        SetupGhOutput(json);
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        issue.Title
            .Should().Be("Login with SSO");

        issue.Body
            .Should().Be("Text.");

        issue.Comments
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{ "title": "Login with SSO", "body": null, "comments": [] }""")]
    [InlineData("""{ "title": "Login with SSO", "body": "", "comments": [] }""")]
    [InlineData("""{ "title": "Login with SSO", "comments": [] }""")]
    public async Task ReadIssueAsync_NullEmptyOrMissingBody_ReturnsEmptyBody(string json)
    {
        // Arrange
        SetupGhOutput(json);
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        issue.Body
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{ "body": "Hi", "createdAt": "2026-10-20T15:30:45Z" }""")]
    [InlineData("""{ "author": null, "body": "Hi", "createdAt": "2026-10-20T15:30:45Z" }""")]
    [InlineData("""{ "author": {}, "body": "Hi", "createdAt": "2026-10-20T15:30:45Z" }""")]
    [InlineData("""{ "author": { "login": null }, "body": "Hi", "createdAt": "2026-10-20T15:30:45Z" }""")]
    [InlineData("""{ "author": { "login": "" }, "body": "Hi", "createdAt": "2026-10-20T15:30:45Z" }""")]
    [InlineData("""{ "author": { "login": "  " }, "body": "Hi", "createdAt": "2026-10-20T15:30:45Z" }""")]
    public async Task ReadIssueAsync_CommentWithoutAuthorLogin_UsesUnknownAuthor(string commentJson)
    {
        // Arrange
        SetupGhOutput($$"""{ "title": "T", "body": "B", "comments": [{{commentJson}}] }""");
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        issue.Comments
            .Should().ContainSingle()
            .Which.Should().Be(new TrackerIssueComment(GitHubCliIssueReader.UnknownAuthor,
                new DateTimeOffset(2026, 10, 20, 15, 30, 45, TimeSpan.Zero), "Hi"));

        GitHubCliIssueReader.UnknownAuthor
            .Should().Be("ghost");
    }

    [Theory]
    [InlineData("""{ "author": { "login": "jane" }, "body": null, "createdAt": "2026-10-20T15:30:45Z" }""")]
    [InlineData("""{ "author": { "login": "jane" }, "createdAt": "2026-10-20T15:30:45Z" }""")]
    public async Task ReadIssueAsync_CommentWithNullOrMissingBody_ReturnsEmptyCommentBody(string commentJson)
    {
        // Arrange
        SetupGhOutput($$"""{ "title": "T", "body": "B", "comments": [{{commentJson}}] }""");
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        issue.Comments
            .Should().ContainSingle()
            .Which.Body.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ \"title\": ")]
    [InlineData("not json")]
    [InlineData("{ \"title\": \"T\" } trailing")]
    public async Task ReadIssueAsync_InvalidJson_ThrowsTrackerIssueReadFailedExceptionWithJsonException(
        string output)
    {
        // Arrange
        SetupGhOutput(output);
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<TrackerIssueReadFailedException>()).Which;

        exception.Reason
            .Should().StartWith("The gh output is not valid JSON: ")
            .And.EndWith(".");

        exception.InnerException
            .Should().BeAssignableTo<JsonException>();
    }

    [Theory]
    [InlineData("""[]""", "The gh output is not a JSON object.")]
    [InlineData("""null""", "The gh output is not a JSON object.")]
    [InlineData("""{ "body": "B", "comments": [] }""", "The gh output has no title string.")]
    [InlineData("""{ "title": null, "comments": [] }""", "The gh output has no title string.")]
    [InlineData("""{ "title": 42, "comments": [] }""", "The gh output has no title string.")]
    [InlineData("""{ "title": "T", "body": 1 }""", "The body of the issue in the gh output is not a string.")]
    [InlineData("""{ "title": "T", "body": {} }""", "The body of the issue in the gh output is not a string.")]
    [InlineData("""{ "title": "T", "comments": {} }""", "The comments in the gh output are not an array.")]
    [InlineData("""{ "title": "T", "comments": "none" }""", "The comments in the gh output are not an array.")]
    [InlineData("""{ "title": "T", "comments": ["text"] }""",
        "The value of comment 1 in the gh output is not a JSON object.")]
    [InlineData("""{ "title": "T", "comments": [{ "author": "jane", "createdAt": "2026-10-20T15:30:45Z" }] }""",
        "The author of comment 1 in the gh output is not a JSON object.")]
    [InlineData("""{ "title": "T", "comments": [{ "author": { "login": 7 }, "createdAt": "2026-10-20T15:30:45Z" }] }""",
        "The author login of comment 1 in the gh output is not a string.")]
    [InlineData("""{ "title": "T", "comments": [{ "author": { "login": "jane" } }] }""",
        "The createdAt time of comment 1 in the gh output is missing or not in ISO 8601 format.")]
    [InlineData("""{ "title": "T", "comments": [{ "createdAt": null }] }""",
        "The createdAt time of comment 1 in the gh output is missing or not in ISO 8601 format.")]
    [InlineData("""{ "title": "T", "comments": [{ "createdAt": 1760974245 }] }""",
        "The createdAt time of comment 1 in the gh output is missing or not in ISO 8601 format.")]
    [InlineData("""{ "title": "T", "comments": [{ "createdAt": "yesterday" }] }""",
        "The createdAt time of comment 1 in the gh output is missing or not in ISO 8601 format.")]
    [InlineData("""{ "title": "T", "comments": [{ "createdAt": "2026-10-20T15:30:45Z", "body": false }] }""",
        "The body of comment 1 in the gh output is not a string.")]
    [InlineData(
        """{ "title": "T", "comments": [{ "createdAt": "2026-10-20T15:30:45Z" }, { "createdAt": "2026-13-01" }] }""",
        "The createdAt time of comment 2 in the gh output is missing or not in ISO 8601 format.")]
    public async Task ReadIssueAsync_UnexpectedJsonShape_ThrowsTrackerIssueReadFailedException(string output,
        string expectedReason)
    {
        // Arrange
        SetupGhOutput(output);
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<TrackerIssueReadFailedException>()).Which;

        exception.Reason
            .Should().Be(expectedReason);

        exception.Message
            .Should().Be($"The issue '{IssueUrl}' cannot be read: {expectedReason}");
    }

    [Theory]
    [InlineData("  GraphQL: Could not resolve to an issue or pull request with the number of 42. (repository.issue)\n",
        "gh exited with the unexpected exit code 1: GraphQL: Could not resolve to an issue or pull request with " +
        "the number of 42. (repository.issue).")]
    [InlineData("To get started with GitHub CLI, please run:  gh auth login\n",
        "gh exited with the unexpected exit code 1: To get started with GitHub CLI, please run:  gh auth login.")]
    [InlineData("HTTP 404: Not Found.\r\n", "gh exited with the unexpected exit code 1: HTTP 404: Not Found.")]
    [InlineData("", "gh exited with the unexpected exit code 1.")]
    [InlineData(" \n ", "gh exited with the unexpected exit code 1.")]
    public async Task ReadIssueAsync_GhExitsWithNonZeroExitCode_ThrowsTrackerIssueReadFailedExceptionWithStandardError(
        string standardError, string expectedReason)
    {
        // Arrange
        A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .Returns(new ExternalProcessResult(1, FullIssueJson, standardError));
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<TrackerIssueReadFailedException>()).Which;

        exception.Reason
            .Should().Be(expectedReason);

        exception.InnerException
            .Should().BeNull();
    }

    [Theory]
    [InlineData(4, "4")]
    [InlineData(-1, "-1")]
    [InlineData(int.MinValue, "-2147483648")]
    public async Task ReadIssueAsync_OtherNonZeroExitCode_NamesExitCodeInInvariantCulture(int exitCode,
        string expectedExitCode)
    {
        // Arrange
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("sv-SE");

        try
        {
            A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._,
                    A<CancellationToken>._))
                .Returns(new ExternalProcessResult(exitCode, string.Empty, "authentication required"));
            var sut = new GitHubCliIssueReader(_ghCommandRunner);

            // Act
            var act = () => sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
                TestContext.Current.CancellationToken);

            // Assert
            (await act.Should().ThrowAsync<TrackerIssueReadFailedException>()).Which.Reason
                .Should().Be($"gh exited with the unexpected exit code {expectedExitCode}: authentication required.");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task ReadIssueAsync_NonZeroExitCodeWithInvalidJson_ReportsExitCodeNotJson()
    {
        // Arrange
        A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .Returns(new ExternalProcessResult(1, "not json", "HTTP 404: Not Found"));
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<TrackerIssueReadFailedException>()).Which.Reason
            .Should().Be("gh exited with the unexpected exit code 1: HTTP 404: Not Found.");
    }

    [Fact]
    public async Task ReadIssueAsync_MultiLineStandardError_KeepsInnerLineBreaks()
    {
        // Arrange
        A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .Returns(new ExternalProcessResult(1, string.Empty, "line one\nline two\n"));
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<TrackerIssueReadFailedException>()).Which.Reason
            .Should().Be("gh exited with the unexpected exit code 1: line one\nline two.");
    }

    [Fact]
    public async Task ReadIssueAsync_ExitCodeZeroWithStandardError_ReturnsIssue()
    {
        // Arrange
        A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .Returns(new ExternalProcessResult(0, FullIssueJson, "A new release of gh is available."));
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        issue.Title
            .Should().Be("Login with SSO");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadIssueAsync_EmptyOrWhitespaceTitle_ReturnsTitleUnchanged(string title)
    {
        // Arrange
        SetupGhOutput($$"""{ "title": "{{title}}", "body": "B", "comments": [] }""");
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        issue.Title
            .Should().Be(title);
    }

    [Fact]
    public async Task ReadIssueAsync_UnknownProperties_IgnoresThem()
    {
        // Arrange
        SetupGhOutput("""{ "title": "T", "number": 42, "labels": [{ "name": "bug" }], "comments": [] }""");
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        issue
            .Should().BeEquivalentTo(new TrackerIssue("T", string.Empty, []));
    }

    [Fact]
    public async Task ReadIssueAsync_NullComment_ThrowsTrackerIssueReadFailedException()
    {
        // Arrange
        SetupGhOutput("""{ "title": "T", "comments": [null] }""");
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<TrackerIssueReadFailedException>()).Which.Reason
            .Should().Be("The value of comment 1 in the gh output is not a JSON object.");
    }

    [Fact]
    public async Task ReadIssueAsync_AuthorLoginWithSurroundingWhitespace_KeepsLoginUnchanged()
    {
        // Arrange
        SetupGhOutput(
            """{ "title": "T", "comments": [{ "author": { "login": " jane " }, "createdAt": "2026-10-20T15:30:45Z" }] }""");
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        issue.Comments
            .Should().ContainSingle()
            .Which.Author.Should().Be(" jane ");
    }

    [Fact]
    public async Task ReadIssueAsync_CreatedAtWithoutOffset_TakesTimeAsUtc()
    {
        // Arrange
        SetupGhOutput("""{ "title": "T", "comments": [{ "createdAt": "2026-10-20T15:30:45" }] }""");
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        var createdAt = issue.Comments.Should().ContainSingle().Which.CreatedAt;

        createdAt
            .Should().Be(new DateTimeOffset(2026, 10, 20, 15, 30, 45, TimeSpan.Zero));

        createdAt.Offset
            .Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task ReadIssueAsync_HttpUrl_RunsGh()
    {
        // Arrange
        const string httpUrl = "http://ghe.example.com/team/app/issues/7";
        SetupGhOutput("""{ "title": "T" }""");
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var issue = await sut.ReadIssueAsync(new Uri(httpUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        issue.Title
            .Should().Be("T");

        A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.Matches(x => x[2] == httpUrl), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReadIssueAsync_UrlThatNeedsEscaping_PassesEscapedAbsoluteUrlToGh()
    {
        // Arrange
        var issueUrl = new Uri("https://github.com/octo/repo/issues/42?q=ä b#issuecomment-1");
        SetupGhOutput("""{ "title": "T" }""");
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        await sut.ReadIssueAsync(issueUrl, WorkingDirectory, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.Matches(x =>
                    x[2] == "https://github.com/octo/repo/issues/42?q=%C3%A4%20b#issuecomment-1"),
                A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReadIssueAsync_GhNotFound_PropagatesGhNotFoundExceptionUnchanged()
    {
        // Arrange
        var ghNotFoundException = new GhNotFoundException(["issue", "view"], "The GitHub CLI gh was not found.");
        A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ThrowsAsync(ghNotFoundException);
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<GhNotFoundException>()).Which
            .Should().BeSameAs(ghNotFoundException);
    }

    [Fact]
    public async Task ReadIssueAsync_CanceledToken_ThrowsOperationCanceledExceptionWithoutRunningGh()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        AssertRunnerNotCalled();
    }

    [Fact]
    public async Task ReadIssueAsync_CanceledWhileGhRuns_PropagatesOperationCanceledExceptionUnwrapped()
    {
        // Arrange
        var canceledException = new OperationCanceledException("canceled");
        A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ThrowsAsync(canceledException);
        var sut = new GitHubCliIssueReader(_ghCommandRunner);

        // Act
        var act = () => sut.ReadIssueAsync(new Uri(IssueUrl), WorkingDirectory,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowExactlyAsync<OperationCanceledException>()).Which
            .Should().BeSameAs(canceledException);
    }

    private void SetupGhOutput(string standardOutput)
    {
        A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .Returns(new ExternalProcessResult(0, standardOutput, string.Empty));
    }

    private void AssertRunnerNotCalled()
    {
        A.CallTo(() => _ghCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
