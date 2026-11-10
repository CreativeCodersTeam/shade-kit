using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Trackers;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands;

/// <summary>
/// Runs shade-kit commands through
/// <see cref="Program.RunAsync(string[], TextWriter, TextWriter, Action{IServiceCollection})" /> against a real temporary
/// repository, for the end-to-end tests of the command groups.
/// </summary>
/// <remarks>
/// git is answered by a fake <see cref="IGitCommandRunner" /> (the current branch is <see cref="Branch" />, the
/// identity is <see cref="UserName" /> and <see cref="UserEmail" />, every other call fails), and the tracker issue
/// reader is a fake that returns the issue <see cref="IssueUrl" />, so no test starts <c>git</c> or <c>gh</c>. The
/// current directory is restored on disposal; a test class that uses the harness must be in the
/// <see cref="CurrentDirectoryCollectionDefinition" /> collection.
/// </remarks>
internal sealed partial class CliEndToEndHarness : IDisposable
{
    public const string IssueUrl = "https://github.com/acme/shop/issues/142";

    public const string IssueFeatureId = "gh-142-login-sso";

    public const string UserName = "Jane Doe";

    public const string UserEmail = "jane@example.com";

    private static readonly string[] CurrentBranchArguments = ["symbolic-ref", "--short", "-q", "HEAD"];

    private readonly CurrentDirectoryScope _currentDirectory = new CurrentDirectoryScope();

    /// <summary>
    /// Initializes a new instance of the <see cref="CliEndToEndHarness" /> class with a new temporary directory.
    /// </summary>
    /// <param name="tempDirectoryPrefix">The prefix of the name of the temporary directory.</param>
    public CliEndToEndHarness(string tempDirectoryPrefix)
    {
        TempDirectory = new TempKitDirectory(tempDirectoryPrefix);

        A.CallTo(() => GitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .ReturnsLazily((string _, IReadOnlyList<string> arguments, CancellationToken _) =>
                Task.FromResult(AnswerGit(arguments)));

        A.CallTo(() => TrackerIssueReader.ReadIssueAsync(A<Uri>._, A<string>._, A<CancellationToken>._))
            .Returns(new TrackerIssue("Login with SSO", "Users sign in with the company account.", []));
    }

    public TempKitDirectory TempDirectory { get; }

    public IGitCommandRunner GitCommandRunner { get; } = A.Fake<IGitCommandRunner>();

    public ITrackerIssueReader TrackerIssueReader { get; } = A.Fake<ITrackerIssueReader>();

    /// <summary>
    /// Gets or sets the current branch that the fake git reports; <see langword="null" /> for a detached HEAD.
    /// </summary>
    public string? Branch { get; set; } = "main";

    /// <summary>
    /// Gets or sets an action that sees the arguments of every git call before it is answered.
    /// </summary>
    public Action<IReadOnlyList<string>>? OnGitCall { get; set; }

    /// <summary>
    /// Gets or sets an action that registers further services of a run, after the fake git runner and the fake
    /// tracker issue reader.
    /// </summary>
    public Action<IServiceCollection>? ConfigureServices { get; set; }

    public void Dispose()
    {
        _currentDirectory.Dispose();
        TempDirectory.Dispose();
    }

    /// <summary>
    /// Makes the temporary directory a git repository root and the current directory, and runs <c>init</c>.
    /// </summary>
    public async Task InitAsync()
    {
        TempDirectory.CreateGitRoot();
        Directory.SetCurrentDirectory(TempDirectory.RootPath);

        (await RunAsync("init", "--json")).ExitCode
            .Should().Be(0);
    }

    /// <summary>
    /// Creates the feature <see cref="IssueFeatureId" /> from <see cref="IssueUrl" />, with the specified profile or
    /// the default profile.
    /// </summary>
    public async Task CreateIssueFeatureAsync(string? profile = null)
    {
        string[] profileArguments = profile is null ? [] : ["--profile", profile];

        (await RunJsonAsync(["feature", "new", "--source", IssueUrl, "--title", "Login SSO", .. profileArguments,
                "--json"]))
            .ExitCode.Should().Be(0);
    }

    /// <summary>
    /// Runs every command in JSON mode and asserts that each one succeeds.
    /// </summary>
    public async Task RunAllOkAsync(params string[][] commands)
    {
        foreach (var command in commands)
        {
            (await RunJsonAsync([.. command, "--json"])).ExitCode
                .Should().Be(0, string.Join(' ', command));
        }
    }

    public async Task<(int ExitCode, string Output, string Error)> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await Program.RunAsync(args, output, error, x =>
        {
            x.AddSingleton(GitCommandRunner)
                .AddSingleton(TrackerIssueReader);

            ConfigureServices?.Invoke(x);
        });

        return (exitCode, output.ToString(), error.ToString());
    }

    public async Task<(int ExitCode, JsonElement Root)> RunJsonAsync(params string[] args)
    {
        var (exitCode, output, _) = await RunAsync(args);

        using var document = JsonDocument.Parse(output);

        return (exitCode, document.RootElement.Clone());
    }

    public string GetKitPath(params string[] parts)
    {
        return Path.Combine([TempDirectory.RootPath, ".shadekit", .. parts]);
    }

    public string GetFeaturePath(string featureId, params string[] parts)
    {
        return GetKitPath(["features", featureId, .. parts]);
    }

    public string GetIssueFeaturePath(params string[] parts)
    {
        return GetFeaturePath(IssueFeatureId, parts);
    }

    public string GetCurrentFeatureFilePath()
    {
        return GetKitPath("features", "current-feature.json");
    }

    /// <summary>
    /// Asserts that a JSON run failed with the specified exit code and error code and wrote no data.
    /// </summary>
    public static void AssertError((int ExitCode, JsonElement Root) run, int exitCode, string errorCode)
    {
        run.ExitCode
            .Should().Be(exitCode);

        run.Root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        run.Root.GetProperty("data").ValueKind
            .Should().Be(JsonValueKind.Null);

        run.Root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(errorCode);
    }

    public static string[] GetLines(string output)
    {
        return [.. output.Split('\n').Select(x => x.TrimEnd('\r'))];
    }

    /// <summary>
    /// Folds every run of whitespace into a single space, because the console wraps long lines.
    /// </summary>
    public static string FoldWhitespace(string text)
    {
        return WhitespaceRegex().Replace(text, " ");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private ExternalProcessResult AnswerGit(IReadOnlyList<string> arguments)
    {
        OnGitCall?.Invoke(arguments);

        if (arguments.SequenceEqual(CurrentBranchArguments))
        {
            return Branch is null
                ? new ExternalProcessResult(1, string.Empty, string.Empty)
                : new ExternalProcessResult(0, Branch + "\n", string.Empty);
        }

        return arguments switch
        {
            ["config", "user.name"] => new ExternalProcessResult(0, UserName + "\n", string.Empty),
            ["config", "user.email"] => new ExternalProcessResult(0, UserEmail + "\n", string.Empty),
            _ => new ExternalProcessResult(128, string.Empty, $"unexpected git call: {string.Join(' ', arguments)}")
        };
    }
}
