using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations;

public class DirectoryPathComparerTests
{
    // A path below the temporary directory that no test creates.
    private static readonly string MissingPath =
        Path.Combine(Path.GetTempPath(), "shade-kit-tests-missing-" + Guid.NewGuid().ToString("N"), "Kit");

    private static readonly DirectoryPathComparer CaseSensitiveComparer = new(StringComparison.Ordinal);

    private static readonly DirectoryPathComparer CaseInsensitiveComparer = new(StringComparison.OrdinalIgnoreCase);

    [Theory]
    [InlineData(true, false, StringComparison.OrdinalIgnoreCase)]
    [InlineData(false, true, StringComparison.OrdinalIgnoreCase)]
    [InlineData(false, false, StringComparison.Ordinal)]
    public void GetComparison_OperatingSystem_IgnoresCaseOnlyOnWindowsAndMacOS(bool isWindows, bool isMacOS,
        StringComparison expected)
    {
        // Act
        var comparison = DirectoryPathComparer.GetComparison(isWindows, isMacOS);

        // Assert
        comparison
            .Should().Be(expected);
    }

    [Fact]
    public void ForCurrentOperatingSystem_Always_UsesComparisonOfCurrentOperatingSystem()
    {
        // Act
        var comparer = DirectoryPathComparer.ForCurrentOperatingSystem();

        // Assert
        comparer.Comparison
            .Should().Be(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    [Fact]
    public void AreSameDirectory_SameExistingDirectory_ReturnsTrue()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var path = tempDirectory.CreateDirectory("kit");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(path, path);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_ExistingDirectoryWithTrailingSeparators_ReturnsTrue()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var path = tempDirectory.CreateDirectory("kit");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(
            path + Path.DirectorySeparatorChar + Path.DirectorySeparatorChar, path);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_MissingDirectoryWithTrailingSeparatorAndDotSegments_ReturnsTrue()
    {
        // Arrange
        var otherForm = Path.Combine(MissingPath, "sub", "..", ".") + Path.DirectorySeparatorChar;

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(otherForm, MissingPath);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_OtherDirectory_ReturnsFalse()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var first = tempDirectory.CreateDirectory("first");
        var second = tempDirectory.CreateDirectory("second");

        // Act
        var isSame = CaseInsensitiveComparer.AreSameDirectory(first, second);

        // Assert
        isSame
            .Should().BeFalse();
    }

    [Fact]
    public void AreSameDirectory_CaseOnlyDifferenceAndCaseInsensitiveComparer_ReturnsTrue()
    {
        // Act
        var isSame = CaseInsensitiveComparer.AreSameDirectory(MissingPath, MissingPath.ToUpperInvariant());

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_CaseOnlyDifferenceAndCaseSensitiveComparer_ReturnsFalse()
    {
        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(MissingPath, MissingPath.ToUpperInvariant());

        // Assert
        isSame
            .Should().BeFalse();
    }

    [Fact]
    public void AreSameDirectory_ExistingDirectoryInOtherCaseThroughSymbolicLink_FollowsComparer()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("kit");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(target, "kit-link");
        var otherCase = Path.Combine(tempDirectory.RootPath, "KIT");

        // Act
        var isSameIgnoringCase = CaseInsensitiveComparer.AreSameDirectory(otherCase, link);
        var isSameWithCase = CaseSensitiveComparer.AreSameDirectory(otherCase, link);

        // Assert
        isSameIgnoringCase
            .Should().BeTrue();

        isSameWithCase
            .Should().BeFalse();
    }

    [Fact]
    public void AreSameDirectory_SymbolicLinkToDirectory_ReturnsTrue()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("kit");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(target, "kit-link");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(link, target);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_SymbolicLinkInParentDirectory_ReturnsTrue()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("work", "kit");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(Path.GetDirectoryName(target)!, "work-link");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(Path.Combine(link, "kit"), target);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_RelativeSymbolicLinkWithParentSegment_ReturnsTrue()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("kit");
        tempDirectory.CreateDirectory("links");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(
            Path.Combine("..", "kit"), "links", "kit-link");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(link, target);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_RelativeLinkTargetLeavingLinkedParentDirectory_CombinesWithResolvedParent()
    {
        // Arrange: alias -> deep/real, and alias/kit-link -> ../../kit leaves deep/real, not alias.
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("kit");
        var real = tempDirectory.CreateDirectory("deep", "real");
        var alias = tempDirectory.CreateDirectorySymbolicLinkOrSkip(real, "alias");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(Path.Combine("..", "..", "kit"), "deep", "real",
            "kit-link");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(Path.Combine(alias, "kit-link"), target);

        // Assert
        Directory.Exists(link)
            .Should().BeTrue();

        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_LinkTargetWithDotSegmentAndTrailingSeparator_ReturnsTrue()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("kit");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(
            "." + Path.DirectorySeparatorChar + "kit" + Path.DirectorySeparatorChar, "kit-link");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(link, target);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_ParentSegmentAfterSymbolicLink_IsRemovedBeforeLinkResolution()
    {
        // Arrange: link -> work/kit; link/../other means <root>/other, as Path.GetFullPath reads it, not work/other.
        using var tempDirectory = new TempKitDirectory();
        var linkTarget = tempDirectory.CreateDirectory("work", "kit");
        tempDirectory.CreateDirectory("work", "other");
        var rootOther = tempDirectory.CreateDirectory("other");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(linkTarget, "kit-link");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(Path.Combine(link, "..", "other"), rootOther);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_ChainOfSymbolicLinks_ReturnsTrue()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("kit");
        var firstLink = tempDirectory.CreateDirectorySymbolicLinkOrSkip(target, "link-1");
        var secondLink = tempDirectory.CreateDirectorySymbolicLinkOrSkip(firstLink, "link-2");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(target, secondLink);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_LinkTargetThatContainsSymbolicLink_ReturnsTrue()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("work", "kit");
        var parentLink = tempDirectory.CreateDirectorySymbolicLinkOrSkip(Path.GetDirectoryName(target)!,
            "work-link");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(Path.Combine(parentLink, "kit"), "kit-link");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(link, target);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_MissingDirectoryBelowSymbolicLink_ComparesWithoutLinkResolution()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("kit");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(target, "kit-link");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(Path.Combine(link, "missing"),
            Path.Combine(target, "missing"));

        // Assert
        isSame
            .Should().BeFalse("a path that does not exist is only normalized");
    }

    [Fact]
    public void AreSameDirectory_SymbolicLinkLoop_ComparesWithoutLinkResolution()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var firstLink = tempDirectory.CreateDirectorySymbolicLinkOrSkip(
            Path.Combine(tempDirectory.RootPath, "loop-2"), "loop-1");
        tempDirectory.CreateDirectorySymbolicLinkOrSkip(firstLink, "loop-2");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(firstLink + Path.DirectorySeparatorChar, firstLink);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void AreSameDirectory_NullFirstPath_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CaseSensitiveComparer.AreSameDirectory(null!, MissingPath);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("path");
    }

    [Fact]
    public void AreSameDirectory_NullSecondPath_ThrowsArgumentNullException()
    {
        // Act
        var act = () => CaseSensitiveComparer.AreSameDirectory(MissingPath, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("path");
    }

    [Theory]
    [InlineData("")]
    [InlineData("kit")]
    [InlineData("./kit")]
    public void AreSameDirectory_RelativePath_ThrowsArgumentException(string relativePath)
    {
        // Act
        var act = () => CaseSensitiveComparer.AreSameDirectory(relativePath, MissingPath);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("path");
    }

    [Fact]
    public void ResolvePath_SymbolicLinkWithTrailingSeparator_ReturnsResolvedTargetWithoutLinks()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var target = tempDirectory.CreateDirectory("kit");
        var link = tempDirectory.CreateDirectorySymbolicLinkOrSkip(target, "kit-link");
        var resolvedTarget = DirectoryPathComparer.ResolvePath(target);

        // Act
        var resolved = DirectoryPathComparer.ResolvePath(link + Path.DirectorySeparatorChar);

        // Assert
        resolved
            .Should().Be(resolvedTarget);

        new DirectoryInfo(resolved).LinkTarget
            .Should().BeNull();
    }

    [Theory]
    [InlineData("/tmp", "/private/tmp")]
    [InlineData("/var", "/private/var")]
    [InlineData("/tmp/", "/private/tmp")]
    public void ResolvePath_MacOSRootLink_ReturnsPrivateDirectory(string path, string expected)
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "Only macOS links /tmp and /var to /private.");
        Assert.SkipWhen(new DirectoryInfo(Path.TrimEndingDirectorySeparator(path)).LinkTarget is null,
            $"'{path}' is no symbolic link here.");

        // Act
        var resolved = DirectoryPathComparer.ResolvePath(path);

        // Assert
        resolved
            .Should().Be(expected);
    }

    [Fact]
    public void ResolvePath_MacOSTempDirectoryBelowVar_ReturnsPathBelowPrivateVar()
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "Only macOS links /var to /private/var.");
        using var tempDirectory = new TempKitDirectory();
        Assert.SkipUnless(tempDirectory.RootPath.StartsWith("/var/", StringComparison.Ordinal),
            $"The temporary directory '{tempDirectory.RootPath}' is not below /var.");

        // Act
        var resolved = DirectoryPathComparer.ResolvePath(tempDirectory.RootPath);

        // Assert
        resolved
            .Should().Be("/private" + tempDirectory.RootPath);
    }

    [Fact]
    public void AreSameDirectory_ForwardSlashesOnWindows_ReturnsTrue()
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows reads '/' as a directory separator besides '\\'.");
        using var tempDirectory = new TempKitDirectory();
        var path = tempDirectory.CreateDirectory("work", "kit");

        // Act
        var isSame = CaseSensitiveComparer.AreSameDirectory(path.Replace('\\', '/'), path);

        // Assert
        isSame
            .Should().BeTrue();
    }

    [Fact]
    public void ResolvePath_RootDirectory_KeepsItsSeparator()
    {
        // Arrange
        var root = Path.GetPathRoot(Path.GetTempPath())!;

        // Act
        var resolved = DirectoryPathComparer.ResolvePath(root);

        // Assert
        resolved
            .Should().Be(root);
    }

    [Fact]
    public void ResolvePath_MissingPath_ReturnsNormalizedPath()
    {
        // Act
        var resolved = DirectoryPathComparer.ResolvePath(MissingPath + Path.DirectorySeparatorChar);

        // Assert
        resolved
            .Should().Be(MissingPath);
    }
}
