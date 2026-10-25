using System.Runtime.Versioning;
using System.Text;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Trackers;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;
using CreativeCoders.ShadeKit.Core.Web;
using CreativeCoders.ShadeKit.Core.Web.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public sealed class FeatureIdFactoryTests : IDisposable
{
    private const string IssueUrl = "https://github.com/acme/shop/issues/142";

    private const string WebUrl = "https://example.com/requests/login-sso";

    private const string WebContent = "# Login SSO\n\nThe customers want to log in with their company account.\n";

    private const string TimePrefix = "20261020-1530";

    private const string SizeLimitReason = "The file is larger than 1,048,576 bytes (1 MiB), the size limit of a " +
                                           "file source. Shorten the file, or pass a shorter request as text.";

    private static readonly TimeZoneInfo UtcPlusTwo =
        TimeZoneInfo.CreateCustomTimeZone("Test UTC+02", TimeSpan.FromHours(2), "Test UTC+02", "Test UTC+02");

    private static readonly Dictionary<string, string> GitHubTrackers = new Dictionary<string, string> { ["github.com"] = "gh" };

    // 2026-10-20 13:30:45.678 UTC is 15:30:45.678 in UTC+02.
    private static readonly DateTimeOffset UtcNow = new DateTimeOffset(2026, 10, 20, 13, 30, 45, 678, TimeSpan.Zero);

    private static readonly TrackerIssue DefaultIssue = new TrackerIssue("Issue Title",
        "The customers want to log in with their company account.",
        [new TrackerIssueComment("jane", new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero), "Agreed.")]);

    private readonly TempKitDirectory _repository = new TempKitDirectory();

    private readonly ITrackerIssueReader _trackerIssueReader = A.Fake<ITrackerIssueReader>();

    private readonly IUrlContentReader _urlContentReader = A.Fake<IUrlContentReader>();

    public FeatureIdFactoryTests()
    {
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Returns(DefaultIssue);

        A.CallTo(() => _urlContentReader.ReadAsync(A<Uri>._, A<int>._, A<CancellationToken>._))
            .Returns(WebContent);
    }

    public void Dispose()
    {
        _repository.Dispose();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC1")]
    public async Task CreateAsync_GitHubIssueUrlWithTitle_ReturnsIssueBasedId()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, IssueUrl, "Login SSO");

        // Assert
        identity
            .Should().BeEquivalentTo(new FeatureIdentity("gh-142-login-sso", "Login SSO",
                new FeatureSource { Kind = FeatureSourceKind.Url, Value = IssueUrl },
                TrackerIssueMarkdown.Format(DefaultIssue),
                new DateTimeOffset(2026, 10, 20, 15, 30, 45, TimeSpan.FromHours(2))),
                options => options.ComparingByMembers<FeatureIdentity>());
    }

    [Fact]
    [Trait("Requirement", "FP03-AC2")]
    public async Task CreateAsync_PlainText_ReturnsTimeBasedIdInLocalTime()
    {
        // Arrange
        const string text = "Login SSO for customers\nThe customers want to log in with their company account.\n";
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, text, null);

        // Assert
        identity
            .Should().BeEquivalentTo(new FeatureIdentity($"{TimePrefix}-login-sso-for-customers",
                "Login SSO for customers", new FeatureSource { Kind = FeatureSourceKind.Text, Value = text }, text,
                new DateTimeOffset(2026, 10, 20, 15, 30, 45, TimeSpan.FromHours(2))),
                options => options.ComparingByMembers<FeatureIdentity>());
    }

    [Fact]
    [Trait("Requirement", "FP03-AC2")]
    public async Task CreateAsync_LocalDateDiffersFromUtcDate_UsesLocalDateAndTimeForId()
    {
        // Arrange
        var factory = CreateFactory(new DateTimeOffset(2026, 10, 20, 23, 5, 0, TimeSpan.Zero));

        // Act
        var identity = await CreateAsync(factory, "Login SSO", null);

        // Assert
        identity.Id
            .Should().Be("20261021-0105-login-sso");

        identity.CreatedAt
            .Should().Be(new DateTimeOffset(2026, 10, 21, 1, 5, 0, TimeSpan.FromHours(2)));

        identity.CreatedAt.Offset
            .Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task CreateAsync_AnySource_TruncatesCreatedAtToWholeSeconds()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "Login SSO", null);

        // Assert
        (identity.CreatedAt.Ticks % TimeSpan.TicksPerSecond)
            .Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_TextWithLeadingEmptyLines_UsesFirstNonEmptyTrimmedLineAsTitle()
    {
        // Arrange
        const string text = "\n  \r\n   Login SSO  \r\nDetails\r\n";
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, text, null);

        // Assert
        identity.Title
            .Should().Be("Login SSO");

        identity.Id
            .Should().Be($"{TimePrefix}-login-sso");

        identity.RequestContent
            .Should().Be(text);
    }

    [Fact]
    public async Task CreateAsync_TextWithTitle_UsesTrimmedTitle()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "Some long request text", "  Login SSO  ");

        // Assert
        identity.Title
            .Should().Be("Login SSO");

        identity.Id
            .Should().Be($"{TimePrefix}-login-sso");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task CreateAsync_TextWithWhitespaceTitle_DerivesTitleFromText(string title)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "Login SSO", title);

        // Assert
        identity.Title
            .Should().Be("Login SSO");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t\r\n")]
    public async Task CreateAsync_EmptyOrWhitespaceSource_ThrowsInvalidFeatureSourceException(string source)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, source, "Login SSO");

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.SourceText
            .Should().Be(source);

        exception.Reason
            .Should().Contain("empty");
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("日本語")]
    public async Task CreateAsync_TextTitleWithoutSlug_ThrowsInvalidFeatureTitleExceptionWithTitleHint(string text)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, text, null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason.Should().Contain($"'{text}'").And.Contain("--title");
    }

    [Fact]
    public async Task CreateAsync_WebUrlWithoutTitle_ThrowsInvalidFeatureTitleExceptionWithoutReadingUrl()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, WebUrl, null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason.Should().Be("A URL source whose host is not a tracker in config.json has no title from " +
                                      "which the feature id can be built. Pass the title with --title.");

        A.CallTo(_urlContentReader).MustNotHaveHappened();
        A.CallTo(_trackerIssueReader).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("???")]
    [InlineData("Login\nSSO")]
    public async Task CreateAsync_WebUrlWithMissingOrUnusableTitle_ThrowsInvalidFeatureTitleExceptionWithoutReadingUrl(
        string title)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, WebUrl, title);

        // Assert
        await act.Should().ThrowAsync<InvalidFeatureTitleException>();

        A.CallTo(_urlContentReader).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("???")]
    [InlineData("Login\nSSO")]
    public async Task CreateAsync_TrackerUrlWithUnusableTitle_ThrowsInvalidFeatureTitleExceptionWithoutReadingIssue(
        string title)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, IssueUrl, title);

        // Assert
        await act.Should().ThrowAsync<InvalidFeatureTitleException>();

        A.CallTo(_trackerIssueReader).MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateAsync_WebUrlWithTitle_ReturnsUrlContentAsRequestContent()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, WebUrl, "Login SSO");

        // Assert
        identity
            .Should().BeEquivalentTo(new FeatureIdentity($"{TimePrefix}-login-sso", "Login SSO",
                new FeatureSource { Kind = FeatureSourceKind.Url, Value = WebUrl }, WebContent,
                new DateTimeOffset(2026, 10, 20, 15, 30, 45, TimeSpan.FromHours(2))),
                options => options.ComparingByMembers<FeatureIdentity>());

        A.CallTo(() => _urlContentReader.ReadAsync(new Uri(WebUrl), FeatureSourceReader.MaxSourceLength, TestContext.Current.CancellationToken))
            .MustHaveHappenedOnceExactly();
        A.CallTo(_trackerIssueReader).MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateAsync_UrlContentReadFails_ThrowsInvalidFeatureSourceExceptionWithReaderException()
    {
        // Arrange
        var readerException = new UrlContentReadFailedException(new Uri(WebUrl),
            "The server answered with status code 404 (Not Found).");
        A.CallTo(() => _urlContentReader.ReadAsync(A<Uri>._, A<int>._, A<CancellationToken>._)).Throws(readerException);
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, WebUrl, "Login SSO");

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.SourceText
            .Should().Be(WebUrl);

        exception.Reason
            .Should().Be("The content of the URL cannot be read: The server answered with status code 404 " +
                         "(Not Found).");

        exception.InnerException
            .Should().BeSameAs(readerException);
    }

    [Fact]
    public async Task CreateAsync_UrlContentReadCanceled_ThrowsOperationCanceledExceptionUnwrapped()
    {
        // Arrange
        var canceledException = new OperationCanceledException("Canceled by the test.");
        A.CallTo(() => _urlContentReader.ReadAsync(A<Uri>._, A<int>._, A<CancellationToken>._)).Throws(canceledException);
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, WebUrl, "Login SSO");

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceledException);
    }

    [Fact]
    public async Task CreateAsync_TrackerUrlWithoutTitle_UsesTrimmedIssueTitle()
    {
        // Arrange
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Returns(DefaultIssue with { Title = "  Login with SSO  " });
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, IssueUrl, null);

        // Assert
        identity.Id
            .Should().Be("gh-142-login-with-sso");

        identity.Title
            .Should().Be("Login with SSO");

        identity.Source
            .Should().BeEquivalentTo(new FeatureSource { Kind = FeatureSourceKind.Url, Value = IssueUrl });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task CreateAsync_TrackerUrlWithWhitespaceTitle_UsesIssueTitle(string title)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, IssueUrl, title);

        // Assert
        identity.Title
            .Should().Be("Issue Title");

        identity.Id
            .Should().Be("gh-142-issue-title");
    }

    [Fact]
    public async Task CreateAsync_IssueTitleWithControlCharactersOnlyAtEdges_UsesTrimmedIssueTitle()
    {
        // Arrange
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Returns(DefaultIssue with { Title = "\tLogin SSO\n" });
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, IssueUrl, null);

        // Assert
        identity.Title
            .Should().Be("Login SSO");

        identity.RequestContent
            .Should().StartWith("# Login SSO\n\nThe customers want to log in with their company account.\n");
    }

    [Fact]
    public async Task CreateAsync_TrackerIssueReaderThrowsOtherException_PassesItThroughUnwrapped()
    {
        // Arrange
        var readerException = new InvalidOperationException("Unexpected.");
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Throws(readerException);
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, IssueUrl, null);

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(readerException);
    }

    [Fact]
    public async Task CreateAsync_UrlContentReaderThrowsOtherException_PassesItThroughUnwrapped()
    {
        // Arrange
        var readerException = new InvalidOperationException("Unexpected.");
        A.CallTo(() => _urlContentReader.ReadAsync(A<Uri>._, A<int>._, A<CancellationToken>._)).Throws(readerException);
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, WebUrl, "Login SSO");

        // Assert
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(readerException);
    }

    [Fact]
    public async Task CreateAsync_TrackerUrlWithTitle_UsesTitleOverIssueTitle()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, IssueUrl, "Login SSO");

        // Assert
        identity.Title
            .Should().Be("Login SSO");

        identity.RequestContent
            .Should().StartWith("# Issue Title\n");
    }

    [Fact]
    public async Task CreateAsync_TrackerUrl_ReturnsIssueAsMarkdownRequestContent()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, IssueUrl, null);

        // Assert
        identity.RequestContent
            .Should().Be("# Issue Title\n\nThe customers want to log in with their company account.\n\n" +
                         "## Comments\n\n### jane · 2026-10-19T09:00:00+00:00\n\nAgreed.\n");

        A.CallTo(_urlContentReader).MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateAsync_TrackerUrl_ReadsIssueOfUrlInRepositoryRoot()
    {
        // Arrange
        var repositoryRootPath = Path.Combine(_repository.RootPath, "root");
        var factory = CreateFactory();

        // Act
        await CreateAsync(factory, IssueUrl, null, repositoryRootPath: repositoryRootPath);

        // Assert
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(new Uri(IssueUrl), repositoryRootPath,
                TestContext.Current.CancellationToken))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateAsync_IssueTitleWithControlCharacter_ThrowsInvalidFeatureTitleExceptionNamingIssueTitle()
    {
        // Arrange
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Returns(DefaultIssue with { Title = "Login\tSSO" });
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, IssueUrl, null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason
            .Should().StartWith("The title taken from the issue title contains the control character U+0009;");
    }

    [Theory]
    [InlineData("???")]
    [InlineData("   ")]
    public async Task CreateAsync_IssueTitleWithoutSlug_ThrowsInvalidFeatureTitleException(string issueTitle)
    {
        // Arrange
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Returns(DefaultIssue with { Title = issueTitle });
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, IssueUrl, null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason.Should().Contain("contains no letter or digit").And.Contain("--title");
    }

    [Fact]
    public async Task CreateAsync_IssueTitleWithoutSlugAndExplicitTitle_UsesExplicitTitle()
    {
        // Arrange
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Returns(DefaultIssue with { Title = "???" });
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, IssueUrl, "Login SSO");

        // Assert
        identity.Id
            .Should().Be("gh-142-login-sso");
    }

    [Fact]
    public async Task CreateAsync_GhNotFound_ThrowsInvalidFeatureSourceExceptionWithReaderException()
    {
        // Arrange
        var readerException = new GhNotFoundException(["issue", "view", IssueUrl],
            "The GitHub CLI gh was not found in any absolute directory of the PATH environment variable.");
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Throws(readerException);
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, IssueUrl, null);

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.SourceText
            .Should().Be(IssueUrl);

        exception.Reason
            .Should().Be("The issue cannot be read: The GitHub CLI gh was not found in any absolute directory of " +
                         "the PATH environment variable.");

        exception.InnerException
            .Should().BeSameAs(readerException);
    }

    [Fact]
    public async Task CreateAsync_TrackerIssueReadFails_ThrowsInvalidFeatureSourceExceptionWithReaderException()
    {
        // Arrange
        var readerException = new TrackerIssueReadFailedException(new Uri(IssueUrl),
            "gh exited with the exit code 1: Could not resolve to an issue.");
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Throws(readerException);
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, IssueUrl, "Login SSO");

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.SourceText
            .Should().Be(IssueUrl);

        exception.Reason
            .Should().Be("The issue cannot be read: gh exited with the exit code 1: Could not resolve to an issue.");

        exception.InnerException
            .Should().BeSameAs(readerException);
    }

    [Fact]
    public async Task CreateAsync_TrackerIssueReadCanceled_ThrowsOperationCanceledExceptionUnwrapped()
    {
        // Arrange
        var canceledException = new OperationCanceledException("Canceled by the test.");
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Throws(canceledException);
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, IssueUrl, null);

        // Assert
        (await act.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().BeSameAs(canceledException);
    }

    [Theory]
    [InlineData("https://github.com/acme/shop/pull/142", "gh-142-login-sso")]
    [InlineData("https://github.com/acme/shop/issues/142/", "gh-142-login-sso")]
    [InlineData("https://github.com/acme/shop/issues/142?tab=comments", "gh-142-login-sso")]
    [InlineData("https://github.com/acme/shop/issues/142#issuecomment-99", "gh-142-login-sso")]
    [InlineData("https://github.com/acme/shop/issues/142?page=7#issuecomment-99", "gh-142-login-sso")]
    [InlineData("https://GitHub.COM/acme/shop/issues/142", "gh-142-login-sso")]
    [InlineData("http://github.com/acme/shop/issues/0142", "gh-0142-login-sso")]
    [InlineData("https://jane@github.com:443/acme/shop/issues/142", "gh-142-login-sso")]
    public async Task CreateAsync_TrackerUrlVariants_ReturnsIssueBasedIdFromLastPathSegment(string url,
        string expectedId)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, url, "Login SSO");

        // Assert
        identity.Id
            .Should().Be(expectedId);

        identity.Source
            .Should().BeEquivalentTo(new FeatureSource { Kind = FeatureSourceKind.Url, Value = url });

        identity.RequestContent
            .Should().Be(TrackerIssueMarkdown.Format(DefaultIssue));
    }

    [Fact]
    public async Task CreateAsync_TrackerKeyWithDifferentCase_MatchesHostIgnoringCase()
    {
        // Arrange
        var factory = CreateFactory();
        var trackers = new Dictionary<string, string> { ["GitHub.Com"] = "gh" };

        // Act
        var identity = await CreateAsync(factory, IssueUrl, "Login SSO", trackers);

        // Assert
        identity.Id
            .Should().Be("gh-142-login-sso");
    }

    [Fact]
    public async Task CreateAsync_GitLabIssueUrl_ReturnsIssueBasedIdWithGitLabPrefix()
    {
        // Arrange
        var factory = CreateFactory();
        var trackers = new Dictionary<string, string> { ["github.com"] = "gh", ["gitlab.com"] = "gl" };

        // Act
        var identity = await CreateAsync(factory, "https://gitlab.com/acme/shop/-/issues/57", "Login SSO",
            trackers);

        // Assert
        identity.Id
            .Should().Be("gl-57-login-sso");
    }

    [Theory]
    [InlineData("https://example.com/acme/shop/issues/142")]
    [InlineData("https://www.github.com/acme/shop/issues/142")]
    [InlineData("https://github.com.evil.example/acme/shop/issues/142")]
    public async Task CreateAsync_UrlHostNotInTrackers_ReturnsTimeBasedIdWithUrlSource(string url)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, url, "Login SSO");

        // Assert
        identity.Id
            .Should().Be($"{TimePrefix}-login-sso");

        identity.Source
            .Should().BeEquivalentTo(new FeatureSource { Kind = FeatureSourceKind.Url, Value = url });

        identity.RequestContent
            .Should().Be(WebContent);

        A.CallTo(_trackerIssueReader).MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateAsync_EmptyTrackers_ReturnsTimeBasedIdForGitHubUrl()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, IssueUrl, "Login SSO", new Dictionary<string, string>());

        // Assert
        identity.Id
            .Should().Be($"{TimePrefix}-login-sso");

        identity.Source.Kind
            .Should().Be(FeatureSourceKind.Url);
    }

    [Theory]
    [InlineData("https://github.com/acme/shop")]
    [InlineData("https://github.com/acme/shop/issues")]
    [InlineData("https://github.com/acme/shop/issues/abc")]
    [InlineData("https://github.com/acme/shop/issues/142a")]
    [InlineData("https://github.com/acme/shop/issues/142/comments")]
    [InlineData("https://github.com/acme/shop/issues?id=142")]
    [InlineData("https://github.com/acme/shop/issues#142")]
    [InlineData("https://github.com/")]
    public async Task CreateAsync_TrackerUrlWithoutIssueNumber_ThrowsInvalidFeatureSourceException(string url)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, url, "Login SSO");

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.SourceText
            .Should().Be(url);

        exception.Reason
            .Should().Contain("github.com");

        A.CallTo(_trackerIssueReader).MustNotHaveHappened();
        A.CallTo(_urlContentReader).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Login\nSSO")]
    public async Task CreateAsync_TrackerUrlWithoutNumberAndAnyTitle_ThrowsInvalidFeatureSourceExceptionBeforeGh(
        string? title)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "https://github.com/acme/shop", title);

        // Assert
        await act.Should().ThrowAsync<InvalidFeatureSourceException>();

        A.CallTo(_trackerIssueReader).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("GH")]
    [InlineData("g_h")]
    [InlineData("")]
    public async Task CreateAsync_TrackerPrefixGivesInvalidId_ThrowsInvalidFeatureIdException(string prefix)
    {
        // Arrange
        var factory = CreateFactory();
        var trackers = new Dictionary<string, string> { ["github.com"] = prefix };

        // Act
        var act = () => CreateAsync(factory, IssueUrl, "Login SSO", trackers);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureIdException>())
            .Which.FeatureId.Should().Be($"{prefix}-142-login-sso");
    }

    [Theory]
    [InlineData("ftp://github.com/acme/shop/issues/142")]
    [InlineData("mailto:jane@example.com")]
    [InlineData("github.com/acme/shop/issues/142")]
    public async Task CreateAsync_NonHttpUriOrRelativeUrl_TreatsSourceAsText(string source)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, source, "Login SSO");

        // Assert
        identity.Id
            .Should().Be($"{TimePrefix}-login-sso");

        identity.Source
            .Should().BeEquivalentTo(new FeatureSource { Kind = FeatureSourceKind.Text, Value = source });

        identity.RequestContent
            .Should().Be(source);
    }

    [Fact]
    public async Task CreateAsync_RelativeFileInsideRepository_ReturnsFileSourceRelativeToRepositoryRoot()
    {
        // Arrange
        const string content = "# Login SSO\n\nThe customers want to log in with their company account.\n";
        _repository.WriteFile(content, "docs", "requests", "login.md");
        var workingDirectory = _repository.CreateDirectory("docs");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, Path.Combine("requests", "login.md"), null,
            workingDirectory: workingDirectory);

        // Assert
        identity
            .Should().BeEquivalentTo(new FeatureIdentity($"{TimePrefix}-login-sso", "Login SSO",
                new FeatureSource { Kind = FeatureSourceKind.File, Value = "docs/requests/login.md" }, content,
                new DateTimeOffset(2026, 10, 20, 15, 30, 45, TimeSpan.FromHours(2))),
                options => options.ComparingByMembers<FeatureIdentity>());
    }

    [Fact]
    public async Task CreateAsync_AbsoluteFileInsideRepository_ReturnsFileSourceRelativeToRepositoryRoot()
    {
        // Arrange
        var filePath = _repository.WriteFile("# Login SSO\n", "request.md");
        using var otherDirectory = new TempKitDirectory();
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, filePath, null, workingDirectory: otherDirectory.RootPath);

        // Assert
        identity.Source
            .Should().BeEquivalentTo(new FeatureSource { Kind = FeatureSourceKind.File, Value = "request.md" });
    }

    [Fact]
    public async Task CreateAsync_FileOutsideRepository_ReturnsFileSourceWithAbsoluteFullPath()
    {
        // Arrange
        using var otherDirectory = new TempKitDirectory();
        var filePath = otherDirectory.WriteFile("# Login SSO\n", "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", null, workingDirectory: otherDirectory.RootPath);

        // Assert
        identity.Source
            .Should().BeEquivalentTo(new FeatureSource
            {
                Kind = FeatureSourceKind.File,
                Value = Path.GetFullPath(filePath)
            });
    }

    [Fact]
    public async Task CreateAsync_FileInSiblingDirectoryWithRepositoryNamePrefix_ReturnsAbsoluteFullPath()
    {
        // Arrange
        var repositoryRootPath = _repository.CreateDirectory("repo");
        var filePath = _repository.WriteFile("# Login SSO\n", "repo-notes", "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, filePath, null, repositoryRootPath: repositoryRootPath);

        // Assert
        identity.Source.Value
            .Should().Be(filePath);
    }

    [Fact]
    public async Task CreateAsync_FileWithUtf8Bom_StripsBomAndKeepsLineEndings()
    {
        // Arrange
        const string content = "# Login SSO\r\n\r\nLine with CRLF\r\nLine with LF\nÜmlaut\r\n";
        _repository.WriteFileBytes([.. TestText.Utf8Bom, .. Encoding.UTF8.GetBytes(content)], "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", null);

        // Assert
        identity.RequestContent
            .Should().Be(content);

        identity.Title
            .Should().Be("Login SSO");
    }

    [Fact]
    public async Task CreateAsync_FileWithoutBom_KeepsContentUnchanged()
    {
        // Arrange
        const string content = "No heading here.\r\nSecond line without line break";
        _repository.WriteFileBytes(Encoding.UTF8.GetBytes(content), "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", null);

        // Assert
        identity.RequestContent
            .Should().Be(content);
    }

    [Theory]
    [InlineData("Intro text\n# Login SSO\n# Second heading\n", "Login SSO")]
    [InlineData("## Overview\n# Login SSO #\n", "Login SSO")]
    [InlineData("   # Indented Login SSO  \n", "Indented Login SSO")]
    [InlineData("#\tTabbed Login SSO\n", "Tabbed Login SSO")]
    [InlineData("#   \n# Login SSO\n", "Login SSO")]
    [InlineData("#Hashtag\n# Login SSO\n", "Login SSO")]
    [InlineData("    # Code block heading\n# Login SSO\n", "Login SSO")]
    [InlineData("# Login SSO\r\nBody\r\n", "Login SSO")]
    [InlineData("# Login with SSO #\n", "Login with SSO")]
    [InlineData("# Foo ##  \n", "Foo")]
    [InlineData("# Foo\t#\t\n", "Foo")]
    [InlineData("# Foo ##\r\n", "Foo")]
    [InlineData("# Foo#\n", "Foo#")]
    [InlineData("# C#\n", "C#")]
    [InlineData("# Foo # bar\n", "Foo # bar")]
    [InlineData("# Foo \\#\n", "Foo \\#")]
    [InlineData("# #\n# Login SSO\n", "Login SSO")]
    [InlineData("# ###  \n# Login SSO\n", "Login SSO")]
    public async Task CreateAsync_FileWithLevelOneHeading_UsesFirstLevelOneHeadingAsTitle(string content,
        string expectedTitle)
    {
        // Arrange
        _repository.WriteFile(content, "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", null);

        // Assert
        identity.Title
            .Should().Be(expectedTitle);
    }

    [Theory]
    [InlineData("```sh\n# install deps\n```\n# Login SSO\n")]
    [InlineData("~~~\n# install deps\n~~~\n# Login SSO\n")]
    [InlineData("````md\n```\n# Inner heading\n```\n````\n# Login SSO\n")]
    [InlineData("~~~\n# install deps\n```\n# Still in block\n~~~\n# Login SSO\n")]
    [InlineData("   ```\n# install deps\n```\n# Login SSO\n")]
    [InlineData("```sh\r\n# install deps\r\n```\r\n# Login SSO\r\n")]
    [InlineData("    ```\n# Login SSO\n")]
    [InlineData("``` sh `x`\n# Login SSO\n")]
    [InlineData("---\n# yaml comment\n---\n# Login SSO\n")]
    [InlineData("---  \n# yaml comment\ntitle: x\n...\n# Login SSO\n")]
    [InlineData("---\r\n# yaml comment\r\n---\r\n# Login SSO\r\n")]
    [InlineData("---\n```\n---\n# Login SSO\n")]
    public async Task CreateAsync_FileWithHeadingInCodeFenceOrFrontMatter_UsesFirstHeadingOutsideThem(
        string content)
    {
        // Arrange
        _repository.WriteFile(content, "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", null);

        // Assert
        identity.Title
            .Should().Be("Login SSO");

        identity.Id
            .Should().Be($"{TimePrefix}-login-sso");
    }

    [Theory]
    [InlineData("```sh\n# install deps\n")]
    [InlineData("Intro\n~~~\n# install deps\n```\n")]
    [InlineData("````\n# install deps\n```\n# Not a heading either\n")]
    public async Task CreateAsync_FileWithUnclosedCodeFence_UsesFileNameWithoutExtensionAsTitle(string content)
    {
        // Arrange
        _repository.WriteFile(content, "login-sso-request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "login-sso-request.md", null);

        // Assert
        identity.Title
            .Should().Be("login-sso-request");
    }

    [Fact]
    public async Task CreateAsync_FileWithHeadingBeforeUnclosedCodeFence_UsesHeadingBeforeFence()
    {
        // Arrange
        _repository.WriteFile("# Login SSO\n```sh\n# install deps\n", "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", null);

        // Assert
        identity.Title
            .Should().Be("Login SSO");
    }

    [Theory]
    [InlineData("---\n# Login SSO\n# Second heading\n")]
    [InlineData("Intro\n---\n# Login SSO\n---\n# Second heading\n")]
    [InlineData("\n---\n# Login SSO\n---\n# Second heading\n")]
    [InlineData(" ---\n# Login SSO\n---\n# Second heading\n")]
    public async Task CreateAsync_FileWithoutLeadingFrontMatter_UsesFirstHeadingAfterDashes(string content)
    {
        // Arrange
        _repository.WriteFile(content, "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", null);

        // Assert
        identity.Title
            .Should().Be("Login SSO");
    }

    [Theory]
    [InlineData("Plain text without heading\n")]
    [InlineData("## Only a level-2 heading\n")]
    [InlineData("#NoSpace\n")]
    [InlineData("# #\n")]
    [InlineData("")]
    public async Task CreateAsync_FileWithoutLevelOneHeading_UsesFileNameWithoutExtensionAsTitle(string content)
    {
        // Arrange
        _repository.WriteFile(content, "login-sso-request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "login-sso-request.md", null);

        // Assert
        identity.Title
            .Should().Be("login-sso-request");

        identity.Id
            .Should().Be($"{TimePrefix}-login-sso-request");
    }

    [Fact]
    public async Task CreateAsync_FileWithTitle_UsesTitleOverHeading()
    {
        // Arrange
        _repository.WriteFile("# Heading title\n", "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", "Login SSO");

        // Assert
        identity.Title
            .Should().Be("Login SSO");

        identity.Id
            .Should().Be($"{TimePrefix}-login-sso");
    }

    [Fact]
    public async Task CreateAsync_FileNameWithoutSlug_ThrowsInvalidFeatureTitleException()
    {
        // Arrange
        _repository.WriteFile("No heading\n", "___.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "___.md", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason.Should().Contain("--title");
    }

    [Fact]
    public async Task CreateAsync_SourceIsDirectory_TreatsSourceAsText()
    {
        // Arrange
        _repository.CreateDirectory("docs");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "docs", null);

        // Assert
        identity.Source.Kind
            .Should().Be(FeatureSourceKind.Text);

        identity.Id
            .Should().Be($"{TimePrefix}-docs");
    }

    [Fact]
    public async Task CreateAsync_SourceIsMissingFile_TreatsSourceAsText()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "missing.md", null);

        // Assert
        identity.Source.Kind
            .Should().Be(FeatureSourceKind.Text);

        identity.Title
            .Should().Be("missing.md");
    }

    [Fact]
    public async Task CreateAsync_TextWithInvalidPathCharacter_TreatsSourceAsText()
    {
        // Arrange
        // The title is passed, because a title derived from this text would contain the control character U+0000.
        const string text = "Login\0SSO";
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, text, "Login SSO");

        // Assert
        identity.Source.Kind
            .Should().Be(FeatureSourceKind.Text);

        identity.RequestContent
            .Should().Be(text);

        identity.Id
            .Should().Be($"{TimePrefix}-login-sso");
    }

    [Fact]
    public async Task CreateAsync_SymbolicLinkToRegularFile_ReturnsFileSourceWithLinkPath()
    {
        // Arrange
        _repository.WriteFile("# Login SSO\n", "request.md");
        _repository.CreateSymbolicLinkOrSkip("request.md", "link.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "link.md", null);

        // Assert
        identity.Source
            .Should().BeEquivalentTo(new FeatureSource { Kind = FeatureSourceKind.File, Value = "link.md" });

        identity.RequestContent
            .Should().Be("# Login SSO\n");

        identity.Title
            .Should().Be("Login SSO");
    }

    [Fact]
    public async Task CreateAsync_SourceIsDevNull_ReturnsFileSourceWithEmptyRequestContentAndFileNameTitle()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "/dev/null is a Unix device.");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "/dev/null", null);

        // Assert
        identity.Source
            .Should().BeEquivalentTo(new FeatureSource { Kind = FeatureSourceKind.File, Value = "/dev/null" });

        identity.RequestContent
            .Should().BeEmpty();

        identity.Title
            .Should().Be("null");

        identity.Id
            .Should().Be($"{TimePrefix}-null");
    }

    [Fact]
    public async Task CreateAsync_SourceIsDevZero_ThrowsInvalidFeatureSourceExceptionNamingSizeLimit()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "/dev/zero is a Unix device.");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "/dev/zero", "Login SSO");

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.SourceText
            .Should().Be("/dev/zero");

        exception.Reason
            .Should().Be(SizeLimitReason);
    }

    [Fact]
    public async Task CreateAsync_FileOfExactlyMaximumSize_ReturnsFileSourceWithCompleteContent()
    {
        // Arrange
        var content = CreateFileContent(FeatureSourceReader.MaxSourceLength);
        _repository.WriteFile(content, "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", null);

        // Assert
        identity.Source
            .Should().BeEquivalentTo(new FeatureSource { Kind = FeatureSourceKind.File, Value = "request.md" });

        identity.RequestContent
            .Should().Be(content);

        identity.Title
            .Should().Be("Login SSO");
    }

    [Fact]
    public async Task CreateAsync_FileWithMultiByteCharactersOverLimitInBytes_ThrowsSizeLimitException()
    {
        // Arrange
        // 'ü' takes two bytes in UTF-8, so the file has fewer characters than the limit but more bytes.
        const string heading = "# Login SSO\n";
        var characterCount = (FeatureSourceReader.MaxSourceLength - heading.Length) / 2 + 1;
        _repository.WriteFile(heading + new string('ü', characterCount), "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.Reason
            .Should().Be(SizeLimitReason);
    }

    [Fact]
    public async Task CreateAsync_FileOfMaximumSizeEndingWithFourByteCharacter_ReturnsCompleteContent()
    {
        // Arrange
        const string lastCharacter = "\U0001F600";
        var content = CreateFileContent(FeatureSourceReader.MaxSourceLength - 4) + lastCharacter;
        _repository.WriteFile(content, "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", null);

        // Assert
        identity.RequestContent
            .Should().Be(content);
    }

    [Fact]
    public async Task CreateAsync_Utf8BomPlusContentOfExactlyMaximumSize_ReturnsContentWithoutBom()
    {
        // Arrange
        var text = CreateFileContent(FeatureSourceReader.MaxSourceLength - 3);
        _repository.WriteFileBytes([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)], "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", null);

        // Assert
        identity.RequestContent
            .Should().Be(text);
    }

    [Fact]
    public async Task CreateAsync_Utf8BomPlusContentOneByteOverMaximumSize_ThrowsSizeLimitException()
    {
        // Arrange
        // The byte order mark counts toward the limit.
        var text = CreateFileContent(FeatureSourceReader.MaxSourceLength - 2);
        _repository.WriteFileBytes([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)], "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.Reason
            .Should().Be(SizeLimitReason);
    }

    [Fact]
    public async Task CreateAsync_FileOverLimitWithNulByte_ThrowsSizeLimitExceptionBeforeEncodingCheck()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes(CreateFileContent(FeatureSourceReader.MaxSourceLength + 1));
        content[^1] = 0x00;
        _repository.WriteFileBytes(content, "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.Reason
            .Should().Be(SizeLimitReason);
    }

    [Theory]
    [InlineData((byte)0x00, "The file contains a NUL byte, so it is not UTF-8 text;")]
    [InlineData((byte)0xFF, "The file contains bytes that are not valid UTF-8.")]
    public async Task CreateAsync_FileOfMaximumSizeWithInvalidLastByte_ThrowsEncodingException(byte lastByte,
        string expectedReasonStart)
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes(CreateFileContent(FeatureSourceReader.MaxSourceLength));
        content[^1] = lastByte;
        _repository.WriteFileBytes(content, "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.Reason
            .Should().StartWith(expectedReasonStart);
    }

    [Fact]
    public async Task CreateAsync_FileLockedByAnotherHandle_ThrowsInvalidFeatureSourceExceptionWithIoError()
    {
        // Arrange
        var filePath = _repository.WriteFile("# Login SSO\n", "request.md");
        using var lockingStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", null);

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.SourceText
            .Should().Be("request.md");

        exception.Reason
            .Should().StartWith("The file cannot be read: ");

        exception.InnerException
            .Should().BeAssignableTo<IOException>();
    }

    [Fact]
    public async Task CreateAsync_FileOneByteLargerThanMaximumSize_ThrowsInvalidFeatureSourceExceptionNamingSizeLimit()
    {
        // Arrange
        _repository.WriteFile(CreateFileContent(FeatureSourceReader.MaxSourceLength + 1), "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", null);

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.SourceText
            .Should().Be("request.md");

        exception.Reason
            .Should().Be(SizeLimitReason);
    }

    [Fact]
    public async Task CreateAsync_SourceIsSymbolicLinkToMissingFile_ThrowsInvalidFeatureSourceExceptionWithIoError()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(),
            "File.Exists is true for a symbolic link whose target does not exist only on Unix (verified).");
        _repository.CreateSymbolicLinkOrSkip("missing.md", "link.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "link.md", "Login SSO");

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.Reason
            .Should().StartWith("The file cannot be read: ");

        exception.InnerException
            .Should().BeOfType<FileNotFoundException>();
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task CreateAsync_FileCannotBeRead_ThrowsInvalidFeatureSourceExceptionWithReasonAndInnerException()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file unreadable.");
        var filePath = _repository.WriteFile("# Login SSO\n", "request.md");
        var originalMode = File.GetUnixFileMode(filePath);
        File.SetUnixFileMode(filePath, UnixFileMode.None);

        try
        {
            SkipWhenReadable(filePath);
            var factory = CreateFactory();

            // Act
            var act = () => CreateAsync(factory, "request.md", null);

            // Assert
            var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

            exception.SourceText
                .Should().Be("request.md");

            exception.Reason
                .Should().StartWith("The file cannot be read: ");

            exception.InnerException
                .Should().BeOfType<UnauthorizedAccessException>();
        }
        finally
        {
            File.SetUnixFileMode(filePath, originalMode);
        }
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x23, 0x00, 0x20, 0x00, 0x4C, 0x00 }, "UTF-16 LE")]
    [InlineData(new byte[] { 0xFE, 0xFF, 0x00, 0x23, 0x00, 0x20, 0x00, 0x4C }, "UTF-16 BE")]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x23, 0x00, 0x00, 0x00 }, "UTF-32 LE")]
    [InlineData(new byte[] { 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x23 }, "UTF-32 BE")]
    public async Task CreateAsync_FileWithUtf16OrUtf32ByteOrderMark_ThrowsInvalidFeatureSourceExceptionNamingEncoding(
        byte[] content, string encodingName)
    {
        // Arrange
        _repository.WriteFileBytes(content, "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.Reason
            .Should().Be($"The file is encoded as {encodingName}, not as UTF-8. Save the file as UTF-8 and run the " +
                         "command again.");
    }

    [Theory]
    [InlineData(new byte[] { 0x23, 0x20, 0x4C, 0x00, 0x6F, 0x00 })]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x4C, 0x6F, 0x67, 0x69, 0x6E, 0x00 })]
    public async Task CreateAsync_FileWithNulByte_ThrowsInvalidFeatureSourceException(byte[] content)
    {
        // Arrange
        _repository.WriteFileBytes(content, "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", "Login SSO");

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.Reason
            .Should().StartWith("The file contains a NUL byte, so it is not UTF-8 text;")
            .And.EndWith("Save the file as UTF-8 and run the command again.");
    }

    [Theory]
    [InlineData(new byte[] { 0x23, 0x20, 0x4C, 0xE4, 0x6E, 0x67, 0x65 })]
    [InlineData(new byte[] { 0x23, 0x20, 0x4C, 0xC3 })]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x23, 0x20, 0xC0, 0xAF })]
    [InlineData(new byte[] { 0x23, 0x20, 0xED, 0xA0, 0x80 })]
    public async Task CreateAsync_FileWithInvalidUtf8_ThrowsInvalidFeatureSourceExceptionWithDecoderInnerException(
        byte[] content)
    {
        // Arrange
        _repository.WriteFileBytes(content, "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", "Login SSO");

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.Reason
            .Should().Be("The file contains bytes that are not valid UTF-8. Save the file as UTF-8 and run the " +
                         "command again.");

        exception.InnerException
            .Should().BeOfType<DecoderFallbackException>();
    }

    [Fact]
    public async Task CreateAsync_FileWithUtf8BomOnly_ReturnsEmptyRequestContentAndFileNameTitle()
    {
        // Arrange
        _repository.WriteFileBytes(TestText.Utf8Bom, "login-sso.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "login-sso.md", null);

        // Assert
        identity.RequestContent
            .Should().BeEmpty();

        identity.Title
            .Should().Be("login-sso");
    }

    [Theory]
    [InlineData("Login\nSSO", "U+000A")]
    [InlineData("Login\r\nSSO", "U+000D")]
    [InlineData("Login\tSSO", "U+0009")]
    [InlineData("Login\u0001SSO", "U+0001")]
    [InlineData("Login\u001BSSO", "U+001B")]
    [InlineData("Login\u007FSSO", "U+007F")]
    [InlineData("Login\u0085SSO", "U+0085")]
    [InlineData("\u0007Login SSO", "U+0007")]
    public async Task CreateAsync_ExplicitTitleWithControlCharacter_ThrowsInvalidFeatureTitleExceptionNamingCharacter(
        string title, string codePoint)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "Some request text", title);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason
            .Should().Be($"The title passed with --title contains the control character {codePoint}; a title must " +
                         "be a single line without tabs or other control characters. Pass such a title with --title.");
    }

    [Fact]
    public async Task CreateAsync_UrlWithTitleWithControlCharacter_ThrowsInvalidFeatureTitleException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, IssueUrl, "Login\nSSO");

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason
            .Should().StartWith("The title passed with --title contains the control character U+000A;");
    }

    [Fact]
    public async Task CreateAsync_FileWithTitleWithControlCharacter_ThrowsInvalidFeatureTitleException()
    {
        // Arrange
        _repository.WriteFile("# Login SSO\n", "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", "Login\tSSO");

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason
            .Should().StartWith("The title passed with --title contains the control character U+0009;");
    }

    [Fact]
    public async Task CreateAsync_TitleWithControlCharactersOnlyAtEdges_UsesTrimmedTitle()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "Some request text", "\n\tLogin SSO\r\n");

        // Assert
        identity.Title
            .Should().Be("Login SSO");
    }

    [Fact]
    public async Task CreateAsync_TitleWithNonControlUnicode_KeepsTitleUnchanged()
    {
        // Arrange
        const string title = "Übersicht – Login 日本 😀 SSO";
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "Some request text", title);

        // Assert
        identity.Title
            .Should().Be(title);

        identity.Id
            .Should().Be($"{TimePrefix}-ubersicht-login-sso");
    }

    [Fact]
    public async Task CreateAsync_TextWhoseFirstLineHasTab_ThrowsInvalidFeatureTitleExceptionNamingFirstLine()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "Login\tSSO\nBody\n", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason
            .Should().StartWith("The title taken from the first line of the text contains the control character " +
                                "U+0009;")
            .And.EndWith("--title.");
    }

    [Fact]
    public async Task CreateAsync_TextWithCrLfAndTabsAfterFirstLine_UsesFirstLineAsTitle()
    {
        // Arrange
        const string text = "Login SSO\r\n\tIndented body\r\n";
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, text, null);

        // Assert
        identity.Title
            .Should().Be("Login SSO");

        identity.RequestContent
            .Should().Be(text);
    }

    [Fact]
    public async Task CreateAsync_FileWhoseHeadingHasControlCharacter_ThrowsInvalidFeatureTitleExceptionNamingHeading()
    {
        // Arrange
        _repository.WriteFile("# Login\u001BSSO\n", "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason
            .Should().StartWith("The title taken from the first level-1 heading of the file contains the control " +
                                "character U+001B;");
    }

    [Fact]
    public async Task CreateAsync_FileNameWithControlCharacter_ThrowsInvalidFeatureTitleExceptionNamingFileName()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows file names cannot contain control characters.");
        _repository.WriteFile("No heading\n", "login\tsso.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "login\tsso.md", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason
            .Should().StartWith("The title taken from the file name contains the control character U+0009;");
    }

    [Fact]
    public async Task CreateAsync_DeviceSourceAndTitleWithControlCharacter_ThrowsSourceExceptionFirst()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "/dev/zero is a Unix device.");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "/dev/zero", "Login\nSSO");

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.Reason
            .Should().Be(SizeLimitReason);
    }

    [Fact]
    public async Task CreateAsync_FileWithNonAsciiName_ReturnsFileSource()
    {
        // Arrange
        _repository.WriteFile("# Login SSO\n", "Übersicht 日本.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "Übersicht 日本.md", null);

        // Assert
        identity.Source
            .Should().BeEquivalentTo(new FeatureSource { Kind = FeatureSourceKind.File, Value = "Übersicht 日本.md" });
    }

    [Fact]
    public async Task CreateAsync_SymbolicLinkToDevZero_ThrowsInvalidFeatureSourceExceptionNamingSizeLimit()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "/dev/zero is a Unix device.");
        _repository.CreateSymbolicLinkOrSkip("/dev/zero", "link.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "link.md", "Login SSO");

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureSourceException>())
            .Which.Reason
            .Should().Be(SizeLimitReason);
    }

    [Fact]
    public async Task CreateAsync_SymbolicLinkToDirectory_TreatsSourceAsText()
    {
        // Arrange
        var directoryPath = _repository.CreateDirectory("docs");
        _repository.CreateDirectorySymbolicLinkOrSkip(directoryPath, "docs-link");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "docs-link", null);

        // Assert
        identity.Source.Kind
            .Should().Be(FeatureSourceKind.Text);
    }

    [Fact]
    public async Task CreateAsync_SymbolicLinkLoop_ThrowsInvalidFeatureSourceExceptionWithIoError()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(),
            "File.Exists is true for a symbolic link loop only on Unix (verified).");
        _repository.CreateSymbolicLinkOrSkip("b.md", "a.md");
        _repository.CreateSymbolicLinkOrSkip("a.md", "b.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "a.md", "Login SSO");

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.Reason
            .Should().StartWith("The file cannot be read: ");

        exception.InnerException
            .Should().BeAssignableTo<IOException>();
    }

    [Fact]
    public async Task CreateAsync_SymbolicLinkToFileWithoutHeading_UsesLinkFileNameAsTitle()
    {
        // Arrange
        _repository.WriteFile("No heading\n", "target.md");
        _repository.CreateSymbolicLinkOrSkip("target.md", "login-sso.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "login-sso.md", null);

        // Assert
        identity.Title
            .Should().Be("login-sso");
    }

    [Fact]
    public async Task CreateAsync_SourceIsSocket_ThrowsInvalidFeatureSourceExceptionWithIoError()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix domain sockets are files only on Unix.");
        var socketPath = Path.Combine(_repository.RootPath, "s.sock");
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix,
            System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);

        try
        {
            socket.Bind(new System.Net.Sockets.UnixDomainSocketEndPoint(socketPath));
        }
        catch (Exception bindException) when (bindException is System.Net.Sockets.SocketException
                                                  or ArgumentException)
        {
            Assert.Skip($"A Unix domain socket cannot be bound at '{socketPath}': {bindException.Message}");
        }

        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, socketPath, "Login SSO");

        // Assert
        var exception = (await act.Should().ThrowAsync<InvalidFeatureSourceException>()).Which;

        exception.Reason
            .Should().StartWith("The file cannot be read: ");

        exception.InnerException
            .Should().BeAssignableTo<IOException>();
    }

    [Fact]
    public async Task CreateAsync_TextLongerThanMaximumPathLength_TreatsSourceAsText()
    {
        // Arrange
        var text = $"Login SSO {new string('x', 300)}/{new string('y', 5000)}";
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, text, null);

        // Assert
        identity.Source.Kind
            .Should().Be(FeatureSourceKind.Text);

        identity.RequestContent
            .Should().Be(text);
    }

    [Fact]
    public async Task CreateAsync_FileWithCarriageReturnOnlyLineEndings_ThrowsInvalidFeatureTitleExceptionNamingHeading()
    {
        // Arrange
        _repository.WriteFile("# Login SSO\rBody\r", "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason
            .Should().StartWith("The title taken from the first level-1 heading of the file contains the control " +
                                "character U+000D;");
    }

    [Fact]
    public async Task CreateAsync_TextWithCarriageReturnOnlyLineEndings_ThrowsInvalidFeatureTitleExceptionNamingFirstLine()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "Login SSO\rBody", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason
            .Should().StartWith("The title taken from the first line of the text contains the control character " +
                                "U+000D;");
    }

    [Fact]
    public async Task CreateAsync_TextWithNulAndWithoutTitle_ThrowsInvalidFeatureTitleExceptionNamingNul()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "Login\0SSO", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason
            .Should().StartWith("The title taken from the first line of the text contains the control character " +
                                "U+0000;");
    }

    [Theory]
    [InlineData(new byte[] { 0x23, 0x20, 0x4C, 0xE4, 0x6E, 0x67, 0x65 })]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x23, 0x00, 0x20, 0x00, 0x4C, 0x00 })]
    public async Task CreateAsync_FileNotUtf8AndTitleWithControlCharacter_ThrowsSourceExceptionFirst(byte[] content)
    {
        // Arrange
        _repository.WriteFileBytes(content, "request.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "request.md", "Login\nSSO");

        // Assert
        await act.Should().ThrowAsync<InvalidFeatureSourceException>();
    }

    [Theory]
    [InlineData("Login\u001FSSO", "U+001F")]
    [InlineData("Login\u009FSSO", "U+009F")]
    [InlineData("\u0001", "U+0001")]
    public async Task CreateAsync_TitleWithControlCharacterAtRangeEdgeOrOnly_ThrowsControlCharacterException(
        string title, string codePoint)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "Some request text", title);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason
            .Should().StartWith($"The title passed with --title contains the control character {codePoint};");
    }

    [Theory]
    [InlineData("Login SSO")]
    [InlineData("Login​SSO")]
    public async Task CreateAsync_TitleWithNonControlFormatOrSpaceCharacter_KeepsTitle(string title)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "Some request text", title);

        // Assert
        identity.Title
            .Should().Be(title);
    }

    [Fact]
    public async Task CreateAsync_FileWithFourByteUtf8WithoutBom_KeepsContentUnchanged()
    {
        // Arrange
        const string content = "# Login 😀 SSO\nBody 𝄞\n";
        _repository.WriteFileBytes(Encoding.UTF8.GetBytes(content), "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, "request.md", null);

        // Assert
        identity.RequestContent
            .Should().Be(content);

        identity.Title
            .Should().Be("Login 😀 SSO");
    }

    [Fact]
    public async Task CreateAsync_CanceledTokenWithFileSource_ThrowsOperationCanceledException()
    {
        // Arrange
        _repository.WriteFile("# Login SSO\n", "request.md");
        var factory = CreateFactory();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var act = () => factory.CreateAsync("request.md", null, _repository.RootPath, GitHubTrackers,
            _repository.RootPath, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("Login SSO")]
    [InlineData(IssueUrl)]
    public async Task CreateAsync_CanceledTokenWithTextOrUrlSource_ThrowsOperationCanceledException(string source)
    {
        // Arrange
        var factory = CreateFactory();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var act = () => factory.CreateAsync(source, "Login SSO", _repository.RootPath, GitHubTrackers,
            _repository.RootPath, cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CreateAsync_NegativeLocalOffset_UsesEarlierLocalDateForId()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 2, 5, 0, TimeSpan.Zero));
        timeProvider.SetLocalTimeZone(
            TimeZoneInfo.CreateCustomTimeZone("Test UTC-05", TimeSpan.FromHours(-5), "Test UTC-05", "Test UTC-05"));
        var factory = new FeatureIdFactory(timeProvider, new FeatureSourceReader(_trackerIssueReader, _urlContentReader));

        // Act
        var identity = await CreateAsync(factory, "Login SSO", null);

        // Assert
        identity.Id
            .Should().Be("20261020-2105-login-sso");

        identity.CreatedAt
            .Should().Be(new DateTimeOffset(2026, 10, 20, 21, 5, 0, TimeSpan.FromHours(-5)));
    }

    [Fact]
    public async Task CreateAsync_TextWhoseFirstLineHasNoSlug_ThrowsInvalidFeatureTitleException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "!!!\nLogin SSO", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason.Should().Contain("'!!!'");
    }

    [Fact]
    public async Task CreateAsync_FileWhoseHeadingHasNoSlug_ThrowsInvalidFeatureTitleExceptionWithoutFileNameFallback()
    {
        // Arrange
        _repository.WriteFile("# !!!\n", "login-sso.md");
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, "login-sso.md", null);

        // Assert
        (await act.Should().ThrowAsync<InvalidFeatureTitleException>())
            .Which.Reason.Should().Contain("'!!!'");
    }

    [Fact]
    public async Task CreateAsync_FileInRepositoryDirectoryStartingWithDots_ReturnsRepositoryRelativeReference()
    {
        // Arrange
        _repository.WriteFile("# Login SSO\n", "..notes", "request.md");
        var factory = CreateFactory();

        // Act
        var identity = await CreateAsync(factory, Path.Combine("..notes", "request.md"), null);

        // Assert
        identity.Source
            .Should().BeEquivalentTo(new FeatureSource
            {
                Kind = FeatureSourceKind.File,
                Value = "..notes/request.md"
            });
    }

    [Fact]
    public async Task CreateAsync_NullSource_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => CreateAsync(factory, null!, "Login SSO");

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("source");
    }

    [Fact]
    public async Task CreateAsync_NullTrackers_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => factory.CreateAsync(IssueUrl, "Login SSO", _repository.RootPath, null!,
            _repository.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("trackers");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CreateAsync_MissingWorkingDirectory_ThrowsArgumentException(string? directory)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => factory.CreateAsync(IssueUrl, "Login SSO", directory!, GitHubTrackers,
            _repository.RootPath, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName("workingDirectory");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CreateAsync_MissingRepositoryRootPath_ThrowsArgumentException(string? rootPath)
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var act = () => factory.CreateAsync(IssueUrl, "Login SSO", _repository.RootPath, GitHubTrackers,
            rootPath!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName("repositoryRootPath");
    }

    [Fact]
    public void Ctor_NullTimeProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeatureIdFactory(null!, new FeatureSourceReader(_trackerIssueReader, _urlContentReader));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("timeProvider");
    }

    [Fact]
    public void Ctor_NullFeatureSourceReader_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeatureIdFactory(TimeProvider.System, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("featureSourceReader");
    }

    // Creates UTF-8 text of exactly the specified number of bytes that starts with the level-1 heading "Login SSO".
    private static string CreateFileContent(int byteCount)
    {
        const string heading = "# Login SSO\n";

        return heading + new string('a', byteCount - heading.Length);
    }

    private static void SkipWhenReadable(string filePath)
    {
        try
        {
            _ = File.ReadAllBytes(filePath);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        Assert.Skip($"'{filePath}' is readable without permissions, for example because the tests run as root.");
    }

    private FeatureIdFactory CreateFactory(DateTimeOffset? utcNow = null)
    {
        var timeProvider = new FakeTimeProvider(utcNow ?? UtcNow);
        timeProvider.SetLocalTimeZone(UtcPlusTwo);

        return new FeatureIdFactory(timeProvider, new FeatureSourceReader(_trackerIssueReader, _urlContentReader));
    }

    private Task<FeatureIdentity> CreateAsync(FeatureIdFactory factory, string source, string? title,
        IReadOnlyDictionary<string, string>? trackers = null, string? workingDirectory = null,
        string? repositoryRootPath = null)
    {
        return factory.CreateAsync(source, title, workingDirectory ?? _repository.RootPath,
            trackers ?? GitHubTrackers, repositoryRootPath ?? _repository.RootPath,
            TestContext.Current.CancellationToken);
    }
}
