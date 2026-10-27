using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Cli.Tests.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Trackers;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;
using CreativeCoders.ShadeKit.Core.Web;
using CreativeCoders.ShadeKit.Core.Web.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Feature;

/// <summary>
/// Runs the <c>shade-kit feature</c> commands through <see cref="Program.RunAsync(string[], TextWriter, TextWriter, Action{IServiceCollection})" />
/// against a real temporary repository; git is answered by a fake <see cref="IGitCommandRunner" />, and the readers of
/// URL sources are fakes, so no test starts <c>gh</c> or sends a request.
/// </summary>
[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed partial class FeatureCommandsEndToEndTests : IDisposable
{
    private const string IssueUrl = "https://github.com/acme/shop/issues/142";

    private const string IssueFeatureId = "gh-142-login-sso";

    private const string WebUrl = "https://example.com/requests/login-sso";

    private const string GhAuthFailureReason =
        "The gh command exited with the exit code 4: To get started with GitHub CLI, please run: gh auth login.";

    private const string GhNotFoundReason =
        "The GitHub CLI gh was not found in any absolute directory of the PATH environment variable.";

    private const string UrlFailureReason =
        "The server answered with the status code 404 (Not Found), not with a success status code (2xx).";

    private const string GhSignInHint =
        "Check the issue URL and that gh is signed in for its host ('gh auth status'), then run the command again.";

    private const string GhAvailabilityHint =
        "Make sure the GitHub CLI gh is installed, on the PATH, and can be executed, then run the command again.";

    private const string UrlHint =
        "Check the URL and run the command again, or save the content to a file and pass the path of the file with " +
        "--source.";

    private const string UserName = "Jane Doe";

    private const string UserEmail = "jane@example.com";

    private static readonly string[] CurrentBranchArguments = ["symbolic-ref", "--short", "-q", "HEAD"];

    private readonly IConsoleConfirmation _confirmation = A.Fake<IConsoleConfirmation>();

    private readonly CurrentDirectoryScope _currentDirectory = new CurrentDirectoryScope();

    private readonly IGitCommandRunner _gitCommandRunner = A.Fake<IGitCommandRunner>();

    private readonly ITrackerIssueReader _trackerIssueReader = A.Fake<ITrackerIssueReader>();

    private readonly IUrlContentReader _urlContentReader = A.Fake<IUrlContentReader>();

    private readonly TempKitDirectory _tempDirectory = new TempKitDirectory("shade-kit-feature-tests-");

    private string? _branch = "main";

    private ICliCancellation? _cancellation;

    private IFeatureStore? _featureStore;

    private Action<IReadOnlyList<string>>? _onGitCall;

    public FeatureCommandsEndToEndTests()
    {
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ReturnsLazily((string _, IReadOnlyList<string> arguments, CancellationToken _) =>
                Task.FromResult(AnswerGit(arguments)));

        A.CallTo(() => _confirmation.CanAsk).Returns(false);

        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Returns(new TrackerIssue("Login with SSO", "Users sign in with the company account.", []));

        A.CallTo(() => _urlContentReader.ReadAsync(A<Uri>._, A<int>._, A<CancellationToken>._))
            .Returns("Login SSO\n");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC1")]
    public async Task FeatureNew_GitHubIssueUrlWithTitle_CreatesIssueBasedFeature()
    {
        // Arrange
        await InitAsync();

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "new", "--source", IssueUrl, "--title", "Login SSO",
            "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");

        data.GetProperty("id").GetString()
            .Should().Be(IssueFeatureId);

        data.GetProperty("source").GetProperty("kind").GetString()
            .Should().Be("url");

        data.GetProperty("source").GetProperty("value").GetString()
            .Should().Be(IssueUrl);

        data.GetProperty("suggestedBranch").GetString()
            .Should().Be($"feature/{IssueFeatureId}");

        data.GetProperty("paths").GetProperty("featureFile").GetString()
            .Should().Be($".shadekit/features/{IssueFeatureId}/feature.json");

        File.Exists(GetFeaturePath(IssueFeatureId, "feature.json"))
            .Should().BeTrue();

        // The line ending depends on the operating system, because the fake git answers no attribute query.
        (await File.ReadAllTextAsync(GetFeaturePath(IssueFeatureId, "request.md"),
                TestContext.Current.CancellationToken)).ReplaceLineEndings("\n")
            .Should().Be("# Login with SSO\n\nUsers sign in with the company account.\n");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC2")]
    public async Task FeatureNew_PlainTextSource_CreatesTimeBasedFeature()
    {
        // Arrange
        await InitAsync();

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "new", "--source", "Login SSO via Azure", "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");

        data.GetProperty("id").GetString()
            .Should().MatchRegex(@"^\d{8}-\d{4}-login-sso-via-azure$");

        data.GetProperty("title").GetString()
            .Should().Be("Login SSO via Azure");

        data.GetProperty("source").GetProperty("kind").GetString()
            .Should().Be("text");

        data.GetProperty("source").GetProperty("value").GetString()
            .Should().Be("Login SSO via Azure");

        var featureId = data.GetProperty("id").GetString()!;

        (await File.ReadAllTextAsync(GetFeaturePath(featureId, "request.md"), TestContext.Current.CancellationToken))
            .Should().Be("Login SSO via Azure");

        File.Exists(Path.Combine(_tempDirectory.RootPath, ".shadekit", "templates", "request.md"))
            .Should().BeFalse();

        A.CallTo(_trackerIssueReader).MustNotHaveHappened();
        A.CallTo(_urlContentReader).MustNotHaveHappened();
    }

    [Fact]
    public async Task FeatureNew_FileSource_WritesFileContentAsRequest()
    {
        // Arrange
        await InitAsync();
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory.RootPath, "login.md"),
            "# Login SSO\n\nUsers sign in with the company account.\n", TestContext.Current.CancellationToken);

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "new", "--source", "login.md", "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");
        var featureId = data.GetProperty("id").GetString()!;

        featureId
            .Should().MatchRegex(@"^\d{8}-\d{4}-login-sso$");

        data.GetProperty("source").GetProperty("kind").GetString()
            .Should().Be("file");

        data.GetProperty("source").GetProperty("value").GetString()
            .Should().Be("login.md");

        // The line ending depends on the operating system, because the fake git answers no attribute query.
        (await File.ReadAllTextAsync(GetFeaturePath(featureId, "request.md"), TestContext.Current.CancellationToken))
            .ReplaceLineEndings("\n")
            .Should().Be("# Login SSO\n\nUsers sign in with the company account.\n");

        A.CallTo(_trackerIssueReader).MustNotHaveHappened();
        A.CallTo(_urlContentReader).MustNotHaveHappened();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC3")]
    public async Task FeatureNew_IdExistsAlready_Returns30AndChangesNothing()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        var snapshot = _tempDirectory.TakeSnapshot();

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "new", "--source", IssueUrl, "--title", "Login SSO",
            "--json");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.FeatureAlreadyExists);

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.FeatureAlreadyExists);

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }


    [Fact]
    public async Task FeatureNew_JsonIssueCannotBeRead_Returns33WithGhSignInHintAndChangesNothing()
    {
        // Arrange
        await InitAsync();
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Throws(new TrackerIssueReadFailedException(new Uri(IssueUrl), GhAuthFailureReason));
        var snapshot = _tempDirectory.TakeSnapshot();

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "new", "--source", IssueUrl, "--json");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidFeatureSource);

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.InvalidFeatureSource);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be($"The source '{IssueUrl}' cannot be used: The issue cannot be read: {GhAuthFailureReason} " +
                         GhSignInHint);

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    public async Task FeatureNew_PlainIssueCannotBeRead_Returns33WithGhSignInHint()
    {
        // Arrange
        await InitAsync();
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Throws(new TrackerIssueReadFailedException(new Uri(IssueUrl), GhAuthFailureReason));

        // Act
        var (exitCode, output, _) = await RunAsync("feature", "new", "--source", IssueUrl);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidFeatureSource);

        GetPlainText(output)
            .Should().Contain($"The issue cannot be read: {GhAuthFailureReason} {GhSignInHint}");

        Directory.Exists(GetFeaturePath(IssueFeatureId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task FeatureNew_JsonGhNotFound_Returns33WithGhAvailabilityHint()
    {
        // Arrange
        await InitAsync();
        A.CallTo(() => _trackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Throws(new GhNotFoundException(["issue", "view", IssueUrl, "--json", "title,body,comments"],
                GhNotFoundReason));

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "new", "--source", IssueUrl, "--json");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidFeatureSource);

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.InvalidFeatureSource);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be($"The source '{IssueUrl}' cannot be used: The issue cannot be read: {GhNotFoundReason} " +
                         GhAvailabilityHint);

        Directory.Exists(GetFeaturePath(IssueFeatureId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task FeatureNew_JsonUrlContentCannotBeRead_Returns33WithUrlHintAndChangesNothing()
    {
        // Arrange
        await InitAsync();
        A.CallTo(() => _urlContentReader.ReadAsync(A<Uri>._, A<int>._, A<CancellationToken>._))
            .Throws(new UrlContentReadFailedException(new Uri(WebUrl), UrlFailureReason));
        var snapshot = _tempDirectory.TakeSnapshot();

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "new", "--source", WebUrl, "--title", "Login SSO",
            "--json");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidFeatureSource);

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.InvalidFeatureSource);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be($"The source '{WebUrl}' cannot be used: The content of the URL cannot be read: " +
                         $"{UrlFailureReason} {UrlHint}");

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    public async Task FeatureNew_PlainUrlContentCannotBeRead_Returns33WithUrlHint()
    {
        // Arrange
        await InitAsync();
        A.CallTo(() => _urlContentReader.ReadAsync(A<Uri>._, A<int>._, A<CancellationToken>._))
            .Throws(new UrlContentReadFailedException(new Uri(WebUrl), UrlFailureReason));

        // Act
        var (exitCode, output, _) = await RunAsync("feature", "new", "--source", WebUrl, "--title", "Login SSO");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidFeatureSource);

        GetPlainText(output)
            .Should().Contain($"The content of the URL cannot be read: {UrlFailureReason} {UrlHint}");
    }

    [Fact]
    public async Task FeatureNew_JsonWebUrlWithTitle_CreatesFeatureWithUrlContentAsRequest()
    {
        // Arrange
        await InitAsync();

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "new", "--source", WebUrl, "--title", "Login SSO",
            "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");
        var featureId = data.GetProperty("id").GetString()!;

        featureId
            .Should().MatchRegex(@"^\d{8}-\d{4}-login-sso$");

        data.GetProperty("source").GetProperty("kind").GetString()
            .Should().Be("url");

        data.GetProperty("source").GetProperty("value").GetString()
            .Should().Be(WebUrl);

        // The line ending depends on the operating system, because the fake git answers no attribute query.
        (await File.ReadAllTextAsync(GetFeaturePath(featureId, "request.md"), TestContext.Current.CancellationToken))
            .ReplaceLineEndings("\n")
            .Should().Be("Login SSO\n");

        A.CallTo(() => _urlContentReader.ReadAsync(new Uri(WebUrl), A<int>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(_trackerIssueReader).MustNotHaveHappened();
    }

    [Fact]
    public async Task FeatureNew_PlainWebUrlWithTitle_CreatesFeature()
    {
        // Arrange
        await InitAsync();

        // Act
        var (exitCode, output, _) = await RunAsync("feature", "new", "--source", WebUrl, "--title", "Login SSO");

        // Assert
        exitCode
            .Should().Be(0);

        GetLines(output)
            .Should().Contain(x => x.StartsWith("Created feature ", StringComparison.Ordinal) &&
                                   x.EndsWith("-login-sso (feature).", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FeatureNew_JsonWebUrlWithoutTitle_Returns34AndReadsNoUrl()
    {
        // Arrange
        await InitAsync();
        var snapshot = _tempDirectory.TakeSnapshot();

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "new", "--source", WebUrl, "--json");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidFeatureTitle);

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.InvalidFeatureTitle);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("Pass the title with --title.");

        A.CallTo(_urlContentReader).MustNotHaveHappened();
        A.CallTo(_trackerIssueReader).MustNotHaveHappened();

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    public async Task FeatureNew_PlainWebUrlWithoutTitle_Returns34AndReadsNoUrl()
    {
        // Arrange
        await InitAsync();

        // Act
        var (exitCode, output, _) = await RunAsync("feature", "new", "--source", WebUrl);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidFeatureTitle);

        GetPlainText(output)
            .Should().Contain("Pass the title with --title.");

        A.CallTo(_urlContentReader).MustNotHaveHappened();
    }
    [Fact]
    [Trait("Requirement", "FP03-AC4")]
    public async Task FeatureCurrent_AfterNewOnUnrelatedBranch_ReturnsNewFeatureFromOverride()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        _branch = "main";

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "current", "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");

        data.GetProperty("resolved").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("resolvedFrom").GetString()
            .Should().Be("override");

        data.GetProperty("feature").GetProperty("id").GetString()
            .Should().Be(IssueFeatureId);

        root.GetProperty("warnings").GetArrayLength()
            .Should().Be(0);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC5")]
    public async Task FeatureCurrent_BranchAndOverrideSelectDifferentFeatures_ReturnsBranchFeatureWithWarning()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        // The second feature is created last, so the local override selects it.
        (await RunJsonAsync("feature", "new", "--source", "https://github.com/acme/shop/issues/7", "--title",
                "Billing", "--json"))
            .ExitCode.Should().Be(0);

        _branch = $"feature/{IssueFeatureId}";

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "current", "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");

        data.GetProperty("resolvedFrom").GetString()
            .Should().Be("branch");

        data.GetProperty("feature").GetProperty("id").GetString()
            .Should().Be(IssueFeatureId);

        FeatureCommandTestHelper.GetWarningCodes(root)
            .Should().Equal(ShadeKitCliWarningCodes.CurrentFeatureMismatch);
    }

    [Theory]
    [Trait("Requirement", "FP03-AC6")]
    [InlineData("main")]
    [InlineData(null)]
    public async Task FeatureCurrent_NoMatchingBranchAndNoOverride_ReturnsNotResolvedWithExitCode0(string? branch)
    {
        // Arrange
        await InitAsync();
        _branch = branch;

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "current", "--json");

        // Assert
        exitCode
            .Should().Be(0);

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        var data = root.GetProperty("data");

        data.GetProperty("resolved").GetBoolean()
            .Should().BeFalse();

        data.GetProperty("resolvedFrom").ValueKind
            .Should().Be(JsonValueKind.Null);

        data.GetProperty("feature").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC7")]
    public async Task FeatureSet_UnknownId_Returns31()
    {
        // Arrange
        await InitAsync();

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "set", "unknown-id", "--json");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.FeatureNotFound);

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.FeatureNotFound);

        File.Exists(GetCurrentFeatureFilePath())
            .Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC7")]
    public async Task FeatureSet_PlainUnknownId_Returns31WithMessage()
    {
        // Arrange
        await InitAsync();

        // Act
        var (exitCode, output, _) = await RunAsync("feature", "set", "unknown-id");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.FeatureNotFound);

        FoldWhitespace(output)
            .Should().Contain("'unknown-id' does not exist");
    }

    [Fact]
    public async Task FeatureSet_ExistingIdAsPositionalArgument_WritesOverride()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        File.Delete(GetCurrentFeatureFilePath());

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "set", IssueFeatureId, "--json");

        // Assert
        exitCode
            .Should().Be(0);

        root.GetProperty("data").GetProperty("id").GetString()
            .Should().Be(IssueFeatureId);

        (await File.ReadAllTextAsync(GetCurrentFeatureFilePath(), TestContext.Current.CancellationToken))
            .Should().Contain($"\"{IssueFeatureId}\"");
    }

    [Fact]
    [Trait("Requirement", "FP03-AC8")]
    public async Task FeatureDrop_JsonWithoutYes_DeletesNothingAndListsFiles()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        var snapshot = _tempDirectory.TakeSnapshot();

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "drop", IssueFeatureId, "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");

        data.GetProperty("dropped").GetBoolean()
            .Should().BeFalse();

        data.GetProperty("overrideAffected").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("overrideCleared").GetBoolean()
            .Should().BeFalse();

        data.GetProperty("files").EnumerateArray().Select(x => x.GetString())
            .Should().Equal(
                $".shadekit/features/{IssueFeatureId}/feature.json",
                $".shadekit/features/{IssueFeatureId}/request.md");

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);

        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC8")]
    public async Task FeatureDrop_JsonWithYes_DeletesFolderAndClearsOverride()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "drop", IssueFeatureId, "--yes", "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");

        data.GetProperty("dropped").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("overrideAffected").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("overrideCleared").GetBoolean()
            .Should().BeTrue();

        data.GetProperty("files").GetArrayLength()
            .Should().Be(2);

        Directory.Exists(GetFeaturePath(IssueFeatureId))
            .Should().BeFalse();

        File.Exists(GetCurrentFeatureFilePath())
            .Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC8")]
    public async Task FeatureDrop_JsonWithoutYesOverrideSelectsOtherFeature_ReturnsOverrideAffectedFalse()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        (await RunJsonAsync("feature", "new", "--source", "Login SSO via Azure", "--json")).ExitCode
            .Should().Be(0);

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "drop", IssueFeatureId, "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");

        data.GetProperty("overrideAffected").GetBoolean()
            .Should().BeFalse();

        data.GetProperty("overrideCleared").GetBoolean()
            .Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC8")]
    public async Task FeatureDrop_JsonWithYesOverrideSelectsOtherFeature_KeepsOverride()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        (await RunJsonAsync("feature", "new", "--source", "Login SSO via Azure", "--json")).ExitCode
            .Should().Be(0);

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "drop", IssueFeatureId, "--yes", "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");

        data.GetProperty("overrideAffected").GetBoolean()
            .Should().BeFalse();

        data.GetProperty("overrideCleared").GetBoolean()
            .Should().BeFalse();

        File.Exists(GetCurrentFeatureFilePath())
            .Should().BeTrue();
    }

    [Fact]
    public async Task FeatureNew_JsonRunCanceled_Returns130WithCanceledEnvelopeAndChangesNothing()
    {
        // Arrange
        await InitAsync();

        var snapshot = _tempDirectory.TakeSnapshot();
        _cancellation = TestCliCancellation.Canceled;

        // Act
        var (exitCode, output, error) = await RunAsync("feature", "new", "--source", IssueUrl, "--title",
            "Login SSO", "--json");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.Canceled);

        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.Canceled);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Be("The command was canceled.");

        error
            .Should().BeEmpty();

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    public async Task FeatureNew_PlainRunCanceled_Returns130WithMessageWithoutStackTrace()
    {
        // Arrange
        await InitAsync();

        var snapshot = _tempDirectory.TakeSnapshot();
        _cancellation = TestCliCancellation.Canceled;

        // Act
        var (exitCode, output, error) = await RunAsync("feature", "new", "--source", IssueUrl, "--title",
            "Login SSO");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.Canceled);

        GetLines(error)
            .Should().Equal("The command was canceled.", string.Empty);

        output
            .Should().BeEmpty();

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    public async Task FeatureNew_CanceledAfterGitIdentity_Returns130AndCreatesNoFeatureFolder()
    {
        // Arrange
        await InitAsync();

        var snapshot = _tempDirectory.TakeSnapshot();
        using var cancellationSource = new CancellationTokenSource();
        _cancellation = new TestCliCancellation(cancellationSource.Token);
        _onGitCall = arguments =>
        {
            if (arguments.SequenceEqual(["config", "user.email"]))
            {
                cancellationSource.Cancel();
            }
        };

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "new", "--source", IssueUrl, "--title", "Login SSO",
            "--json");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.Canceled);

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.Canceled);

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    public async Task FeatureDrop_JsonWithYesRunCanceled_Returns130AndKeepsFeature()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        var snapshot = _tempDirectory.TakeSnapshot();
        _cancellation = TestCliCancellation.Canceled;

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "drop", IssueFeatureId, "--yes", "--json");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.Canceled);

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.Canceled);

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    public async Task FeatureDrop_PlainWithYesRunCanceled_Returns130WithMessageAndKeepsFeature()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        var snapshot = _tempDirectory.TakeSnapshot();
        _cancellation = TestCliCancellation.Canceled;

        // Act
        var (exitCode, output, error) = await RunAsync("feature", "drop", IssueFeatureId, "--yes");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.Canceled);

        GetLines(error)
            .Should().Equal("The command was canceled.", string.Empty);

        output
            .Should().BeEmpty();

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    public async Task FeatureShow_MissingIdWithRunCanceled_ReturnsInvalidArguments()
    {
        // Arrange
        await InitAsync();

        _cancellation = TestCliCancellation.Canceled;

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "show", "--json");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidArguments);

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.InvalidArguments);
    }

    [Fact]
    public async Task FeatureList_OperationCanceledWithoutCanceledToken_ReturnsHostErrorWithDetails()
    {
        // Arrange
        await InitAsync();

        var featureStore = A.Fake<IFeatureStore>();
        A.CallTo(() => featureStore.ListAsync(A<IKitStore>._, A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException("stopped"));
        _featureStore = featureStore;

        // Act
        var (exitCode, output, error) = await RunAsync("feature", "list", "--json");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.HostError);

        using var document = JsonDocument.Parse(output);

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.HostError);

        error
            .Should().Contain("The command failed unexpectedly: stopped")
            .And.Contain(nameof(OperationCanceledException));
    }

    [Fact]
    public async Task FeatureDrop_ShortYesSwitch_DeletesFolder()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        // Act
        var (exitCode, output, _) = await RunAsync("feature", "drop", IssueFeatureId, "-y");

        // Assert
        exitCode
            .Should().Be(0);

        GetLines(output)
            .Should().Contain($"Dropped feature {IssueFeatureId}.");

        Directory.Exists(GetFeaturePath(IssueFeatureId))
            .Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC9")]
    public async Task FeatureShow_AfterNew_ReturnsGitUserAsCreatedBy()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "show", IssueFeatureId, "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");

        data.GetProperty("createdBy").GetProperty("name").GetString()
            .Should().Be(UserName);

        data.GetProperty("createdBy").GetProperty("email").GetString()
            .Should().Be(UserEmail);

        data.GetProperty("id").GetString()
            .Should().Be(IssueFeatureId);

        using var featureFile = JsonDocument.Parse(await File.ReadAllTextAsync(
            GetFeaturePath(IssueFeatureId, "feature.json"), TestContext.Current.CancellationToken));

        featureFile.RootElement.GetProperty("createdBy").GetProperty("name").GetString()
            .Should().Be(UserName);

        featureFile.RootElement.GetProperty("createdBy").GetProperty("email").GetString()
            .Should().Be(UserEmail);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC10")]
    public async Task FeatureDrop_PlainInteractiveConfirmed_DeletesFolder()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        A.CallTo(() => _confirmation.CanAsk).Returns(true);
        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(true);

        // Act
        var (exitCode, output, _) = await RunAsync("feature", "drop", IssueFeatureId);

        // Assert
        exitCode
            .Should().Be(0);

        A.CallTo(() => _confirmation.ConfirmAsync($"Drop feature {IssueFeatureId}?", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        GetLines(output)
            .Should().Contain($"Dropped feature {IssueFeatureId}.");

        Directory.Exists(GetFeaturePath(IssueFeatureId))
            .Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FP03-AC10")]
    public async Task FeatureDrop_PlainInteractiveDeclined_KeepsFolder()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        A.CallTo(() => _confirmation.CanAsk).Returns(true);
        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._)).Returns(false);

        var snapshot = _tempDirectory.TakeSnapshot();

        // Act
        var (exitCode, output, _) = await RunAsync("feature", "drop", IssueFeatureId);

        // Assert
        exitCode
            .Should().Be(0);

        GetLines(output)
            .Should().Contain("Nothing was dropped.");

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC10")]
    public async Task FeatureDrop_PlainRedirectedInput_KeepsFolderWithoutAsking()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        A.CallTo(() => _confirmation.CanAsk).Returns(false);

        var snapshot = _tempDirectory.TakeSnapshot();

        // Act
        var (exitCode, output, _) = await RunAsync("feature", "drop", IssueFeatureId);

        // Assert
        exitCode
            .Should().Be(0);

        A.CallTo(() => _confirmation.ConfirmAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

        FoldWhitespace(output)
            .Should().Contain($"Feature {IssueFeatureId} would be dropped.")
            .And.Contain($"Nothing was dropped. Run 'shade-kit feature drop {IssueFeatureId} --yes' to drop it.");

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    [Trait("Requirement", "FP03-AC11")]
    public async Task FeatureCurrent_StatusInFeatureFile_ReturnsActiveAfterNewAndDoneAfterChange()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();

        // Act
        var (activeExitCode, activeRoot) = await RunJsonAsync("feature", "current", "--json");

        // A done feature needs a consistent end state; 'phase complete' (package 04, task 6) writes the same fields.
        await EditFeatureFileAsync(IssueFeatureId, feature =>
        {
            feature["status"] = "done";
            feature["currentPhase"] = "finish";
            feature["phases"] = new JsonObject
            {
                ["new"] = "approved", ["refine"] = "approved", ["map"] = "approved", ["plan"] = "approved",
                ["implement"] = "approved", ["verify"] = "approved", ["finish"] = "approved"
            };
        });

        var (doneExitCode, doneRoot) = await RunJsonAsync("feature", "current", "--json");

        // Assert
        activeExitCode
            .Should().Be(0);

        activeRoot.GetProperty("data").GetProperty("feature").GetProperty("status").GetString()
            .Should().Be("active");

        doneExitCode
            .Should().Be(0);

        doneRoot.GetProperty("data").GetProperty("feature").GetProperty("status").GetString()
            .Should().Be("done");

        doneRoot.GetProperty("data").GetProperty("feature").GetProperty("currentPhase").GetString()
            .Should().Be("finish");

        doneRoot.GetProperty("data").GetProperty("feature").GetProperty("phaseState").GetString()
            .Should().Be("approved");
    }

    [Fact]
    public async Task FeatureList_TwoFeatures_ReturnsThemInOrdinalOrderWithStatus()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        (await RunJsonAsync("feature", "new", "--source", "https://github.com/acme/shop/issues/7", "--title",
                "Billing", "--profile", "bugfix", "--json"))
            .ExitCode.Should().Be(0);

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "list", "--json");

        // Assert
        exitCode
            .Should().Be(0);

        var features = root.GetProperty("data").GetProperty("features").EnumerateArray().ToArray();

        features.Select(x => x.GetProperty("id").GetString())
            .Should().Equal("gh-142-login-sso", "gh-7-billing");

        features.Select(x => x.GetProperty("profile").GetString())
            .Should().Equal("feature", "bugfix");

        features.Select(x => x.GetProperty("status").GetString())
            .Should().AllBe("active");
    }

    [Fact]
    public async Task PlainMode_EveryFeatureCommand_WritesExpectedLines()
    {
        // Arrange
        await InitAsync();

        // Act
        var newRun = await RunAsync("feature", "new", "-s", IssueUrl, "-t", "Login SSO", "-p", "quick");
        var listRun = await RunAsync("feature", "list");
        var currentRun = await RunAsync("feature", "current");
        var setRun = await RunAsync("feature", "set", IssueFeatureId);
        var showRun = await RunAsync("feature", "show", IssueFeatureId);
        var dropRun = await RunAsync("feature", "drop", IssueFeatureId, "--yes");
        var emptyListRun = await RunAsync("feature", "list");
        var emptyCurrentRun = await RunAsync("feature", "current");

        // Assert
        new[]
            {
                newRun.ExitCode, listRun.ExitCode, currentRun.ExitCode, setRun.ExitCode, showRun.ExitCode,
                dropRun.ExitCode, emptyListRun.ExitCode, emptyCurrentRun.ExitCode
            }
            .Should().AllBeEquivalentTo(0);

        GetLines(newRun.Output)
            .Should().ContainInConsecutiveOrder(
                $"Created feature {IssueFeatureId} (quick).",
                $"  .shadekit/features/{IssueFeatureId}/feature.json",
                $"  .shadekit/features/{IssueFeatureId}/request.md",
                $"Suggested branch: feature/{IssueFeatureId}");

        GetLines(listRun.Output)
            .Should().Contain($"{IssueFeatureId}  active  quick  new  Login SSO");

        GetLines(currentRun.Output)
            .Should().ContainInConsecutiveOrder($"{IssueFeatureId} (quick, active)", "Current phase: new (in-progress)",
                "Resolved from: override");

        GetLines(setRun.Output)
            .Should().Contain($"Current feature: {IssueFeatureId}.");

        GetLines(showRun.Output)
            .Should().Contain($"Id: {IssueFeatureId}")
            .And.Contain("Current phase: new")
            .And.ContainInConsecutiveOrder("Phases:", "  new: in-progress", "  quick: pending", "  finish: pending",
                "History: none")
            .And.Contain($"Source: url {IssueUrl}")
            .And.Contain(x => x.StartsWith("Created: ", StringComparison.Ordinal) &&
                              x.EndsWith($" by {UserName} <{UserEmail}>", StringComparison.Ordinal));

        GetLines(dropRun.Output)
            .Should().ContainInConsecutiveOrder($"Dropped feature {IssueFeatureId}.",
                "The local override pointed to this feature and was cleared.");

        GetLines(emptyListRun.Output)
            .Should().Contain("No features found.");

        GetLines(emptyCurrentRun.Output)
            .Should().ContainInConsecutiveOrder("No current feature.",
                "Run 'shade-kit feature set <id>' to select one.");
    }

    [Fact]
    public async Task FeatureShow_PlainUnknownId_Returns31()
    {
        // Arrange
        await InitAsync();

        // Act
        var (exitCode, output, _) = await RunAsync("feature", "show", "unknown-id");

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.FeatureNotFound);

        FoldWhitespace(output)
            .Should().Contain("'unknown-id' does not exist");
    }

    [Fact]
    public async Task FeatureShowAndList_CopiedFeatureFolder_UseFolderNameAsIdWithIdMismatchWarning()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        CopyDirectory(GetFeaturePath(IssueFeatureId), GetFeaturePath("copy-of-login"));

        // Act
        var (showExitCode, show) = await RunJsonAsync("feature", "show", "copy-of-login", "--json");
        var (listExitCode, list) = await RunJsonAsync("feature", "list", "--json");

        // Assert
        showExitCode
            .Should().Be(0);

        show.GetProperty("data").GetProperty("id").GetString()
            .Should().Be("copy-of-login");

        show.GetProperty("data").GetProperty("paths").GetProperty("featureDir").GetString()
            .Should().Be(".shadekit/features/copy-of-login/");

        FeatureCommandTestHelper.GetWarningCodes(show)
            .Should().Equal(ShadeKitCliWarningCodes.FeatureIdMismatch);

        listExitCode
            .Should().Be(0);

        list.GetProperty("data").GetProperty("features").EnumerateArray()
            .Select(x => x.GetProperty("id").GetString())
            .Should().Equal("copy-of-login", IssueFeatureId);

        FeatureCommandTestHelper.GetWarningCodes(list)
            .Should().Equal(ShadeKitCliWarningCodes.FeatureIdMismatch);
    }

    [Fact]
    public async Task FeatureList_InvalidAndHiddenFolders_SkipsThemWithFolderInvalidWarningOnlyForInvalidName()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        CopyDirectory(GetFeaturePath(IssueFeatureId), GetFeaturePath("Draft"));
        CopyDirectory(GetFeaturePath(IssueFeatureId), GetFeaturePath($".{IssueFeatureId}.1a2b3c4d.dropping"));

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "list", "--json");

        // Assert
        exitCode
            .Should().Be(0);

        root.GetProperty("data").GetProperty("features").EnumerateArray()
            .Select(x => x.GetProperty("id").GetString())
            .Should().Equal(IssueFeatureId);

        FeatureCommandTestHelper.GetWarningCodes(root)
            .Should().Equal(ShadeKitCliWarningCodes.FeatureFolderInvalid);
    }

    [Fact]
    public async Task FeatureCurrent_BrokenOverrideAndBranchFeature_ReturnsBranchFeatureWithInvalidWarning()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        await File.WriteAllTextAsync(GetCurrentFeatureFilePath(), "{", TestContext.Current.CancellationToken);
        _branch = $"feature/{IssueFeatureId}";

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "current", "--json");

        // Assert
        exitCode
            .Should().Be(0);

        root.GetProperty("data").GetProperty("feature").GetProperty("id").GetString()
            .Should().Be(IssueFeatureId);

        FeatureCommandTestHelper.GetWarningCodes(root)
            .Should().Equal(ShadeKitCliWarningCodes.CurrentFeatureInvalid);

        root.GetProperty("warnings")[0].GetProperty("message").GetString()
            .Should().StartWith("The local override '.shadekit/features/current-feature.json' cannot be used and " +
                                "is ignored: ")
            .And.EndWith("Run 'shade-kit feature set <id>' to replace it.");

        (await File.ReadAllTextAsync(GetCurrentFeatureFilePath(), TestContext.Current.CancellationToken))
            .Should().Be("{");
    }

    [Fact]
    public async Task FeatureSetAndNew_TooNewOverride_ReplaceOverride()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        const string tooNewOverride = """{ "schemaVersion": 99, "minEngineVersion": "99.0.0", "featureId": "x" }""";
        await File.WriteAllTextAsync(GetCurrentFeatureFilePath(), tooNewOverride,
            TestContext.Current.CancellationToken);

        // Act
        var (setExitCode, _) = await RunJsonAsync("feature", "set", IssueFeatureId, "--json");
        var (currentAfterSetExitCode, currentAfterSet) = await RunJsonAsync("feature", "current", "--json");

        await File.WriteAllTextAsync(GetCurrentFeatureFilePath(), tooNewOverride,
            TestContext.Current.CancellationToken);
        var (newExitCode, _) = await RunJsonAsync("feature", "new", "--source",
            "https://github.com/acme/shop/issues/7", "--title", "Billing", "--json");
        var (currentAfterNewExitCode, currentAfterNew) = await RunJsonAsync("feature", "current", "--json");

        // Assert
        setExitCode
            .Should().Be(0);

        currentAfterSetExitCode
            .Should().Be(0);

        currentAfterSet.GetProperty("data").GetProperty("feature").GetProperty("id").GetString()
            .Should().Be(IssueFeatureId);

        newExitCode
            .Should().Be(0);

        currentAfterNewExitCode
            .Should().Be(0);

        currentAfterNew.GetProperty("data").GetProperty("feature").GetProperty("id").GetString()
            .Should().Be("gh-7-billing");

        FeatureCommandTestHelper.GetWarningCodes(currentAfterNew)
            .Should().BeEmpty();
    }

    [Theory]
    [Trait("Requirement", "FP04-AC1")]
    [InlineData("feature", new[] { "new", "refine", "map", "plan", "implement", "verify", "finish" })]
    [InlineData("bugfix", new[] { "new", "diagnose", "plan", "implement", "verify", "finish" })]
    [InlineData("quick", new[] { "new", "quick", "finish" })]
    public async Task FeatureNew_EveryProfile_WritesInitialPhaseFieldsAndReadingCommandsReturnThem(string profile,
        string[] phases)
    {
        // Arrange
        await InitAsync();

        // Act
        var (newExitCode, _) = await RunJsonAsync("feature", "new", "--source", IssueUrl, "--title", "Login SSO",
            "--profile", profile, "--json");
        var (showExitCode, show) = await RunJsonAsync("feature", "show", IssueFeatureId, "--json");
        var (listExitCode, list) = await RunJsonAsync("feature", "list", "--json");
        var (currentExitCode, current) = await RunJsonAsync("feature", "current", "--json");

        // Assert
        new[] { newExitCode, showExitCode, listExitCode, currentExitCode }
            .Should().AllBeEquivalentTo(0);

        string[] expectedPhases = [$"{phases[0]}=in-progress", .. phases[1..].Select(x => $"{x}=pending")];

        using var featureFile = JsonDocument.Parse(await File.ReadAllTextAsync(
            GetFeaturePath(IssueFeatureId, "feature.json"), TestContext.Current.CancellationToken));
        var stored = featureFile.RootElement;

        FeatureCommandTestHelper.GetPropertyNames(stored)
            .Should().Equal("schemaVersion", "minEngineVersion", "id", "title", "profile", "source", "createdAt",
                "createdBy", "status", "currentPhase", "phases", "history");

        stored.GetProperty("currentPhase").GetString()
            .Should().Be("new");

        GetPhaseEntries(stored.GetProperty("phases"))
            .Should().Equal(expectedPhases);

        stored.GetProperty("history").GetArrayLength()
            .Should().Be(0);

        var shown = show.GetProperty("data");

        shown.GetProperty("currentPhase").GetString()
            .Should().Be("new");

        shown.GetProperty("pendingTransition").ValueKind
            .Should().Be(JsonValueKind.Null);

        GetPhaseEntries(shown.GetProperty("phases"))
            .Should().Equal(expectedPhases);

        shown.GetProperty("history").GetArrayLength()
            .Should().Be(0);

        list.GetProperty("data").GetProperty("features")[0].GetProperty("currentPhase").GetString()
            .Should().Be("new");

        current.GetProperty("data").GetProperty("feature").GetProperty("currentPhase").GetString()
            .Should().Be("new");

        current.GetProperty("data").GetProperty("feature").GetProperty("phaseState").GetString()
            .Should().Be("in-progress");
    }

    [Fact]
    public async Task ReadingFeatureCommands_FeatureWithoutPhaseFields_ReturnInitialStateAndLeaveFileUnchanged()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        await EditFeatureFileAsync(IssueFeatureId, feature =>
        {
            feature.Remove("currentPhase");
            feature.Remove("phases");
            feature.Remove("history");
        });
        var featureFilePath = GetFeaturePath(IssueFeatureId, "feature.json");
        var bytesBefore = await File.ReadAllBytesAsync(featureFilePath, TestContext.Current.CancellationToken);

        // Act
        var (showExitCode, show) = await RunJsonAsync("feature", "show", IssueFeatureId, "--json");
        var (listExitCode, list) = await RunJsonAsync("feature", "list", "--json");
        var (currentExitCode, current) = await RunJsonAsync("feature", "current", "--json");
        var plainShow = await RunAsync("feature", "show", IssueFeatureId);

        // Assert
        new[] { showExitCode, listExitCode, currentExitCode, plainShow.ExitCode }
            .Should().AllBeEquivalentTo(0);

        show.GetProperty("data").GetProperty("currentPhase").GetString()
            .Should().Be("new");

        GetPhaseEntries(show.GetProperty("data").GetProperty("phases"))
            .Should().Equal("new=in-progress", "refine=pending", "map=pending", "plan=pending", "implement=pending",
                "verify=pending", "finish=pending");

        show.GetProperty("data").GetProperty("history").GetArrayLength()
            .Should().Be(0);

        list.GetProperty("data").GetProperty("features")[0].GetProperty("currentPhase").GetString()
            .Should().Be("new");

        current.GetProperty("data").GetProperty("feature").GetProperty("phaseState").GetString()
            .Should().Be("in-progress");

        GetLines(plainShow.Output)
            .Should().Contain("Current phase: new").And.Contain("History: none");

        (await File.ReadAllBytesAsync(featureFilePath, TestContext.Current.CancellationToken))
            .Should().Equal(bytesBefore);
    }

    [Fact]
    public async Task FeatureShow_PendingTransitionAndHistory_ReturnsThemAsStored()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        await EditFeatureFileAsync(IssueFeatureId, feature =>
        {
            feature["currentPhase"] = "refine";
            feature["phases"] = new JsonObject { ["new"] = "approved", ["refine"] = "awaiting-approval" };
            feature["pendingTransition"] = "map";
            feature["history"] = new JsonArray
            {
                CreateHistoryNode("2026-10-21T09:00:00+02:00", "start", "new", "refine", null),
                CreateHistoryNode("2026-10-21T09:05:00+02:00", "approve", "new", "refine", "looks good"),
                CreateHistoryNode("2026-10-21T11:00:00+02:00", "start", "refine", "map", null)
            };
        });
        var featureFilePath = GetFeaturePath(IssueFeatureId, "feature.json");
        var bytesBefore = await File.ReadAllBytesAsync(featureFilePath, TestContext.Current.CancellationToken);

        // Act
        var (exitCode, root) = await RunJsonAsync("feature", "show", IssueFeatureId, "--json");
        var plainShow = await RunAsync("feature", "show", IssueFeatureId);

        // Assert
        exitCode
            .Should().Be(0);

        var data = root.GetProperty("data");

        data.GetProperty("currentPhase").GetString()
            .Should().Be("refine");

        data.GetProperty("pendingTransition").GetString()
            .Should().Be("map");

        GetPhaseEntries(data.GetProperty("phases"))
            .Should().Equal("new=approved", "refine=awaiting-approval", "map=pending", "plan=pending",
                "implement=pending", "verify=pending", "finish=pending");

        var approve = data.GetProperty("history")[1];

        FeatureCommandTestHelper.GetPropertyNames(approve)
            .Should().Equal("at", "by", "action", "from", "to", "note");

        approve.GetProperty("at").GetString()
            .Should().Be("2026-10-21T09:05:00+02:00");

        approve.GetProperty("by").GetProperty("name").GetString()
            .Should().Be(UserName);

        approve.GetProperty("by").GetProperty("email").GetString()
            .Should().Be(UserEmail);

        approve.GetProperty("action").GetString()
            .Should().Be("approve");

        approve.GetProperty("from").GetString()
            .Should().Be("new");

        approve.GetProperty("to").GetString()
            .Should().Be("refine");

        approve.GetProperty("note").GetString()
            .Should().Be("looks good");

        plainShow.ExitCode
            .Should().Be(0);

        GetLines(plainShow.Output)
            .Should().Contain("Pending transition: map");

        // The console wraps the long history line, so its parts are folded again.
        FoldWhitespace(plainShow.Output)
            .Should().Contain(
                $" 2026-10-21T09:05:00+02:00 approve new -> refine by {UserName} <{UserEmail}>, note: looks good ");

        (await File.ReadAllBytesAsync(featureFilePath, TestContext.Current.CancellationToken))
            .Should().Equal(bytesBefore);
    }

    [Fact]
    public async Task ReadingFeatureCommands_UnknownProfileInFeatureFile_Return40NamingTheFile()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        await EditFeatureFileAsync(IssueFeatureId, feature => feature["profile"] = "legacy");

        // Act
        var (listExitCode, list) = await RunJsonAsync("feature", "list", "--json");
        var (showExitCode, show) = await RunJsonAsync("feature", "show", IssueFeatureId, "--json");
        var (currentExitCode, current) = await RunJsonAsync("feature", "current", "--json");

        // Assert
        new[] { listExitCode, showExitCode, currentExitCode }
            .Should().AllBeEquivalentTo(ShadeKitCliExitCodes.UnknownProfile);

        foreach (var root in new[] { list, show, current })
        {
            root.GetProperty("error").GetProperty("code").GetString()
                .Should().Be(ShadeKitCliErrorCodes.UnknownProfile);

            root.GetProperty("error").GetProperty("message").GetString()
                .Should().Contain($"'.shadekit/features/{IssueFeatureId}/feature.json'").And.Contain("'legacy'");
        }
    }

    [Fact]
    public async Task ReadingFeatureCommands_InvalidPhaseState_Return46AndLeaveFileUnchanged()
    {
        // Arrange
        await InitAsync();
        await CreateIssueFeatureAsync();
        await EditFeatureFileAsync(IssueFeatureId, feature => feature["currentPhase"] = "review");
        var featureFilePath = GetFeaturePath(IssueFeatureId, "feature.json");
        var bytesBefore = await File.ReadAllBytesAsync(featureFilePath, TestContext.Current.CancellationToken);

        // Act
        var (listExitCode, list) = await RunJsonAsync("feature", "list", "--json");
        var (showExitCode, show) = await RunJsonAsync("feature", "show", IssueFeatureId, "--json");
        var (currentExitCode, current) = await RunJsonAsync("feature", "current", "--json");

        // Assert
        new[] { listExitCode, showExitCode, currentExitCode }
            .Should().AllBeEquivalentTo(ShadeKitCliExitCodes.PhaseStateInvalid);

        foreach (var root in new[] { list, show, current })
        {
            root.GetProperty("error").GetProperty("code").GetString()
                .Should().Be(ShadeKitCliErrorCodes.PhaseStateInvalid);

            root.GetProperty("error").GetProperty("message").GetString()
                .Should().Contain($"'.shadekit/features/{IssueFeatureId}/feature.json'")
                .And.Contain("'currentPhase'");
        }

        (await File.ReadAllBytesAsync(featureFilePath, TestContext.Current.CancellationToken))
            .Should().Equal(bytesBefore);
    }

    public void Dispose()
    {
        _currentDirectory.Dispose();
        _tempDirectory.Dispose();
    }

    private ExternalProcessResult AnswerGit(IReadOnlyList<string> arguments)
    {
        _onGitCall?.Invoke(arguments);

        if (arguments.SequenceEqual(CurrentBranchArguments))
        {
            return _branch is null
                ? new ExternalProcessResult(1, string.Empty, string.Empty)
                : new ExternalProcessResult(0, _branch + "\n", string.Empty);
        }

        return arguments switch
        {
            ["config", "user.name"] => new ExternalProcessResult(0, UserName + "\n", string.Empty),
            ["config", "user.email"] => new ExternalProcessResult(0, UserEmail + "\n", string.Empty),
            _ => new ExternalProcessResult(128, string.Empty, $"unexpected git call: {string.Join(' ', arguments)}")
        };
    }

    private async Task InitAsync()
    {
        _tempDirectory.CreateGitRoot();
        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        (await RunAsync("init", "--json")).ExitCode
            .Should().Be(0);
    }

    private async Task CreateIssueFeatureAsync()
    {
        (await RunJsonAsync("feature", "new", "--source", IssueUrl, "--title", "Login SSO", "--json")).ExitCode
            .Should().Be(0);
    }

    private async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(args, output, error, x =>
        {
            x.AddSingleton(_gitCommandRunner)
                .AddSingleton(_confirmation)
                .AddSingleton(_trackerIssueReader)
                .AddSingleton(_urlContentReader);

            if (_cancellation is not null)
            {
                x.AddSingleton(_cancellation);
            }

            if (_featureStore is not null)
            {
                x.AddSingleton(_featureStore);
            }
        });

        return (exitCode, output.ToString(), error.ToString());
    }

    private async Task<(int ExitCode, JsonElement Root)> RunJsonAsync(params string[] args)
    {
        var (exitCode, output, _) = await RunAsync(args);

        using var document = JsonDocument.Parse(output);

        return (exitCode, document.RootElement.Clone());
    }

    private string GetFeaturePath(string featureId, params string[] parts)
    {
        return Path.Combine([_tempDirectory.RootPath, ".shadekit", "features", featureId, .. parts]);
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var filePath in Directory.EnumerateFiles(sourcePath))
        {
            File.Copy(filePath, Path.Combine(destinationPath, Path.GetFileName(filePath)));
        }
    }

    private async Task EditFeatureFileAsync(string featureId, Action<JsonObject> edit)
    {
        var path = GetFeaturePath(featureId, "feature.json");
        var feature = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))!
            .AsObject();

        edit(feature);

        await File.WriteAllTextAsync(path, feature.ToJsonString(), TestContext.Current.CancellationToken);
    }

    private static JsonObject CreateHistoryNode(string at, string action, string from, string to, string? note)
    {
        var entry = new JsonObject
        {
            ["at"] = at,
            ["by"] = new JsonObject { ["name"] = UserName, ["email"] = UserEmail },
            ["action"] = action,
            ["from"] = from,
            ["to"] = to
        };

        if (note is not null)
        {
            entry["note"] = note;
        }

        return entry;
    }

    private static string[] GetPhaseEntries(JsonElement phases)
    {
        return [.. phases.EnumerateObject().Select(x => $"{x.Name}={x.Value.GetString()}")];
    }

    private string GetCurrentFeatureFilePath()
    {
        return Path.Combine(_tempDirectory.RootPath, ".shadekit", "features", "current-feature.json");
    }

    private static string[] GetLines(string output)
    {
        return [.. output.Split('\n').Select(x => x.TrimEnd('\r'))];
    }

    private static string FoldWhitespace(string text)
    {
        // The console wraps long lines, so line breaks are folded back into single spaces.
        return WhitespaceRegex().Replace(text, " ");
    }

    private static string GetPlainText(string output)
    {
        // Error messages are colored per console line, so the escape sequences are removed before the line breaks
        // are folded.
        return FoldWhitespace(AnsiEscapeSequenceRegex().Replace(output, string.Empty));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\x1B\[[0-9;]*m")]
    private static partial Regex AnsiEscapeSequenceRegex();
}
