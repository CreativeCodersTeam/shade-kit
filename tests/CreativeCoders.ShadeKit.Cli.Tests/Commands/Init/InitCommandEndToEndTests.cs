using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Templates;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Init;

/// <summary>
/// Runs <c>shade-kit init</c> through <see cref="Program.RunAsync" /> against a real temporary repository.
/// </summary>
[Collection(CurrentDirectoryCollectionDefinition.Name)]
public sealed partial class InitCommandEndToEndTests : IDisposable
{
    private readonly CurrentDirectoryScope _currentDirectory = new CurrentDirectoryScope();

    private readonly TempKitDirectory _tempDirectory = new TempKitDirectory("shade-kit-init-tests-");

    [Fact]
    [Trait("Requirement", "FP02-AC1")]
    [Trait("Requirement", "FP02-AC8")]
    public async Task RunAsync_InitJsonInGitRepository_CreatesKitFilesAndListsThemInWriteOrder()
    {
        // Arrange
        var repositoryRoot = EnterGitRepository();

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["init", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        root.GetProperty("data").GetProperty("repositoryRoot").GetString()
            .Should().Be(repositoryRoot);

        var files = root.GetProperty("data").GetProperty("files").EnumerateArray()
            .Select(x => (Path: x.GetProperty("path").GetString(), Action: x.GetProperty("action").GetString()))
            .ToArray();

        string[] expectedPaths =
        [
            ".shadekit/config.json",
            ".shadekit/gates.json",
            .. ShippedTemplates.All.Select(x => x.RepositoryPath),
            ".shadekit/templates.lock.json",
            ".shadekit/features/",
            ".gitignore",
            "CLAUDE.md",
            "AGENTS.md"
        ];

        files.Select(x => x.Path)
            .Should().Equal(expectedPaths);

        files.Select(x => x.Action)
            .Should().AllBe("created");

        files.Select(x => Path.Combine(_tempDirectory.RootPath, x.Path!))
            .Should().AllSatisfy(x => Path.Exists(x).Should().BeTrue(x));

        Directory.Exists(Path.Combine(_tempDirectory.RootPath, ".shadekit", "features"))
            .Should().BeTrue();

        error.ToString()
            .Should().BeNullOrWhiteSpace("the host writes only its empty footer line to standard error");
    }

    [Fact]
    [Trait("Requirement", "FP02-AC6")]
    public async Task RunAsync_InitJsonInInitialisedRepository_Returns13WithUpgradeHintAndChangesNoFile()
    {
        // Arrange
        EnterGitRepository();

        (await Program.RunAsync(["init", "--json"], TextWriter.Null, TextWriter.Null))
            .Should().Be(0);

        var snapshot = _tempDirectory.TakeSnapshot();

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["init", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.KitAlreadyInitialized);

        using var document = JsonDocument.Parse(output.ToString());
        var root = document.RootElement;

        root.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        root.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.KitAlreadyInitialized);

        root.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("shade-kit upgrade");

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC6")]
    public async Task RunAsync_InitPlainInInitialisedRepository_PrintsUpgradeHintWithoutJsonAndChangesNoFile()
    {
        // Arrange
        EnterGitRepository();

        (await Program.RunAsync(["init"], TextWriter.Null, TextWriter.Null))
            .Should().Be(0);

        var snapshot = _tempDirectory.TakeSnapshot();

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["init"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.KitAlreadyInitialized);

        // The console wraps long lines, so line breaks are folded back into single spaces.
        var text = WhitespaceRegex().Replace(output.ToString(), " ");

        text.Should().Contain("already initialised")
            .And.Contain("shade-kit upgrade")
            .And.NotContain("\"ok\"");

        _tempDirectory.TakeSnapshot()
            .Should().Equal(snapshot);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC4")]
    [Trait("Requirement", "FP02-AC5")]
    public async Task RunAsync_InitJsonWithExistingRepositoryFiles_ReportsAppendedAndUpdatedFiles()
    {
        // Arrange
        EnterGitRepository();
        var cancellationToken = TestContext.Current.CancellationToken;

        await File.WriteAllTextAsync(Path.Combine(_tempDirectory.RootPath, ".gitignore"), "bin/\n",
            cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory.RootPath, "CLAUDE.md"),
            "# Project\n<!-- shade-kit:begin old -->\nold\n<!-- shade-kit:end -->\n", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_tempDirectory.RootPath, "AGENTS.md"), "# Agents\n",
            cancellationToken);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["init", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("data").GetProperty("files").EnumerateArray()
            .Select(x => (x.GetProperty("path").GetString(), x.GetProperty("action").GetString()))
            .TakeLast(3)
            .Should().Equal((".gitignore", "appended"), ("CLAUDE.md", "updated"), ("AGENTS.md", "appended"));
    }

    [Fact]
    public async Task RunAsync_InitJsonInSubdirectory_InitialisesRepositoryRoot()
    {
        // Arrange
        var repositoryRoot = EnterGitRepository();
        Directory.SetCurrentDirectory(Directory.CreateDirectory(Path.Combine(repositoryRoot, "src", "app")).FullName);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["init", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("data").GetProperty("repositoryRoot").GetString()
            .Should().Be(repositoryRoot);

        File.Exists(Path.Combine(repositoryRoot, ".shadekit", "config.json"))
            .Should().BeTrue();

        Path.Exists(Path.Combine(repositoryRoot, "src", "app", ".shadekit"))
            .Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FP02-AC7")]
    public async Task RunAsync_InitJsonOutsideGitRepository_Returns12AndCreatesNothing()
    {
        // Arrange
        _tempDirectory.SkipWhenGitRootExistsAbove();

        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["init", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.NotAGitRepository);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.NotAGitRepository);

        Directory.EnumerateFileSystemEntries(_tempDirectory.RootPath)
            .Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_InitJsonWithInvalidSectionMarkers_Returns23AndCreatesNoKitRoot()
    {
        // Arrange
        EnterGitRepository();
        var claudePath = Path.Combine(_tempDirectory.RootPath, "CLAUDE.md");
        await File.WriteAllTextAsync(claudePath, "# Project\n<!-- shade-kit:begin -->\n",
            TestContext.Current.CancellationToken);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["init", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.InvalidManagedSection);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.InvalidManagedSection);

        document.RootElement.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("CLAUDE.md");

        Path.Exists(Path.Combine(_tempDirectory.RootPath, ".shadekit"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_InitJsonWithUtf16GitIgnore_Returns24AndChangesNoFile()
    {
        // Arrange
        EnterGitRepository();
        var gitIgnorePath = Path.Combine(_tempDirectory.RootPath, ".gitignore");
        await File.WriteAllBytesAsync(gitIgnorePath, [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("bin/\r\n")],
            TestContext.Current.CancellationToken);
        var snapshot = _tempDirectory.TakeSnapshot();

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["init", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.UnsupportedFileEncoding);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.UnsupportedFileEncoding);

        document.RootElement.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain(".gitignore")
            .And.Contain("UTF-16 LE");

        _tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Theory]
    [InlineData("init")]
    [InlineData("feature list")]
    public async Task RunAsync_KitRootIsFile_Returns15AndChangesNoFile(string command)
    {
        // Arrange
        EnterGitRepository();
        _tempDirectory.WriteFile("not a directory", ".shadekit");
        var snapshot = _tempDirectory.TakeSnapshot();

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync([.. command.Split(' '), "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.KitRootNotADirectory);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.KitRootNotADirectory);

        document.RootElement.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("shade-kit init")
            .And.NotContain("upgrade");

        _tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public async Task RunAsync_InitJsonWithClaudeMdDirectory_Returns14AndChangesNoFile()
    {
        // Arrange
        EnterGitRepository();
        _tempDirectory.CreateDirectory("CLAUDE.md");
        var snapshot = _tempDirectory.TakeSnapshot();

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["init", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(ShadeKitCliExitCodes.PathNotAFile);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeFalse();

        document.RootElement.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(ShadeKitCliErrorCodes.PathNotAFile);

        document.RootElement.GetProperty("error").GetProperty("message").GetString()
            .Should().Contain("CLAUDE.md");

        _tempDirectory.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    [Trait("Requirement", "FP02-AC1")]
    public async Task RunAsync_FeatureListJsonAfterInit_ReturnsEmptyFeatureList()
    {
        // Arrange
        EnterGitRepository();

        (await Program.RunAsync(["init", "--json"], TextWriter.Null, TextWriter.Null))
            .Should().Be(0);

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["feature", "list", "--json"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        using var document = JsonDocument.Parse(output.ToString());

        document.RootElement.GetProperty("ok").GetBoolean()
            .Should().BeTrue();

        document.RootElement.GetProperty("data").GetProperty("features").GetArrayLength()
            .Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_InitPlain_PrintsSummaryAndReturns0()
    {
        // Arrange
        EnterGitRepository();

        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await Program.RunAsync(["init"], output, error);

        // Assert
        exitCode
            .Should().Be(0);

        var lines = output.ToString().Split(Environment.NewLine);

        lines[0]
            .Should().StartWith("Initialised SHADE kit in ");

        lines
            .Should().Contain(["created  .shadekit/config.json", "created  .gitignore", "created  AGENTS.md"])
            .And.Contain(x => x.StartsWith("Commit '.shadekit/'", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _currentDirectory.Dispose();
        _tempDirectory.Dispose();
    }

    /// <summary>
    /// Marks the temporary directory as a git repository root, makes it the current directory, and returns the
    /// current directory as the process sees it (on macOS, the temporary directory resolves to <c>/private/var</c>).
    /// </summary>
    private string EnterGitRepository()
    {
        _tempDirectory.CreateGitRoot();
        Directory.SetCurrentDirectory(_tempDirectory.RootPath);

        return Directory.GetCurrentDirectory();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
