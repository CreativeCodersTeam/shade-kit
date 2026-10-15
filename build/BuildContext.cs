using Cake.Common.Build;
using Cake.Common.Build.GitHubActions.Data;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using CreativeCoders.CakeBuild;
using CreativeCoders.CakeBuild.Tasks.Defaults;
using CreativeCoders.CakeBuild.Tasks.Templates.Settings;

namespace Build;

// Package metadata (project url, license) comes from Directory.Build.props, so the pack settings keep their defaults.
public class BuildContext(ICakeContext context)
    : CakeBuildContext(context), IDefaultTaskSettings, ICreateDistPackagesTaskSettings, ICreateGitHubReleaseTaskSettings
{
    private const string DistPackagePrefix = "shade-kit-";

    public bool UseMicrosoftTestingPlatform => true;

    // Only release tags go to nuget.org; every other build pushes to GitHub Packages.
    public string NuGetFeedUrl => this.GitHubActions().Environment.Workflow.RefType == GitHubActionsRefType.Tag
        ? "nuget.org"
        : "https://nuget.pkg.github.com/CreativeCodersTeam/index.json";

    public bool SkipPush => this.BuildSystem().IsPullRequest ||
                            this.BuildSystem().IsLocalBuild ||
                            this.GitHubActions().Environment.Runner.OS != "Linux";

    public FilePath CliProjectFile =>
        RootDir.CombineWithFilePath("src/CreativeCoders.ShadeKit.Cli/CreativeCoders.ShadeKit.Cli.csproj");

    public DirectoryPath PublishOutputDir => ArtifactsDir.Combine("published");

    /// <summary>
    /// The runtime identifiers to publish the single-file binary for: every <c>--runtime=&lt;rid&gt;</c> argument,
    /// or the runtimes of the host operating system when none is given.
    /// </summary>
    public IReadOnlyList<string> Runtimes => Arguments.HasArgument("runtime")
        ? Arguments.GetArguments("runtime").ToArray()
        : TargetRuntimes.ForHostOperatingSystem();

    public IEnumerable<PublishingItem> PublishingItems => Runtimes
        .Select(runtime => new PublishingItem(CliProjectFile, GetPublishDir(runtime))
        {
            Runtime = runtime,
            SelfContained = true
        });

    public IEnumerable<DistPackage> DistPackages => Runtimes
        .Select(runtime => new DistPackage(DistPackagePrefix + runtime, GetPublishDir(runtime))
        {
            Format = TargetRuntimes.IsWindows(runtime) ? DistPackageFormat.Zip : DistPackageFormat.TarGz
        });

    public string ReleaseName => ReleaseVersion;

    public string ReleaseVersion => $"v{Version.SemVer}";

    public string ReleaseBody => "SHADE kit Release";

    public bool IsPreRelease => !string.IsNullOrWhiteSpace(Version.PreReleaseTag);

    public IEnumerable<GitHubReleaseAsset> ReleaseAssets =>
    [
        .. FindFiles(GetRequiredSettings<ICreateDistPackagesTaskSettings>().DistOutputPath, "*.tar.gz"),
        .. FindFiles(GetRequiredSettings<ICreateDistPackagesTaskSettings>().DistOutputPath, "*.zip"),
        .. FindFiles(GetRequiredSettings<IPackTaskSettings>().PackOutputDirectory, "*.nupkg")
    ];

    public DirectoryPath GetPublishDir(string runtime)
    {
        return PublishOutputDir.Combine(runtime);
    }

    private IEnumerable<GitHubReleaseAsset> FindFiles(DirectoryPath directory, string pattern)
    {
        return this.GetFiles(directory.FullPath + "/" + pattern)
            .Select(file => new GitHubReleaseFileAsset(file.FullPath, null));
    }
}
