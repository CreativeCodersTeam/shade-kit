using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public sealed class DiscussionNotesMoveTests
{
    [Fact]
    public void Create_FileDirectlyInDiscussions_ReturnsMoveWithOriginalAndFeaturePaths()
    {
        // Arrange
        var source = new FeatureSource
        {
            Kind = FeatureSourceKind.File, Value = ".shadekit/discussions/20261020-1500-login-sso.md"
        };

        // Act
        var move = DiscussionNotesMove.Create(source, "notes.md");

        // Assert
        move.OriginalPath
            .Should().Be(".shadekit/discussions/20261020-1500-login-sso.md");

        move.KitRelativeOriginalPath
            .Should().Be("discussions/20261020-1500-login-sso.md");

        move.FeatureRelativePath
            .Should().Be("discussions/20261020-1500-login-sso.md");
    }

    [Fact]
    public void CreateMovedSource_FeatureId_ReturnsFileSourceInFeatureDiscussions()
    {
        // Arrange
        var move = DiscussionNotesMove.Create(
            new FeatureSource { Kind = FeatureSourceKind.File, Value = ".shadekit/discussions/login.md" },
            "notes.md");

        // Act
        var source = move.CreateMovedSource("20261020-1530-login-sso");

        // Assert
        source.Kind
            .Should().Be(FeatureSourceKind.File);

        source.Value
            .Should().Be(".shadekit/features/20261020-1530-login-sso/discussions/login.md");
    }

    [Theory]
    [InlineData(FeatureSourceKind.Text, "Login SSO")]
    [InlineData(FeatureSourceKind.Url, "https://example.com/.shadekit/discussions/login.md")]
    [InlineData(FeatureSourceKind.File, "docs/login.md")]
    [InlineData(FeatureSourceKind.File, ".shadekit/discussions/archive/login.md")]
    [InlineData(FeatureSourceKind.File, ".shadekit/features/x/discussions/login.md")]
    [InlineData(FeatureSourceKind.File, ".shadekit/Discussions/login.md")]
    [InlineData(FeatureSourceKind.File, ".shadekit/discussions")]
    [InlineData(FeatureSourceKind.File, ".shadekit/discussions/")]
    [InlineData(FeatureSourceKind.File, "/tmp/.shadekit/discussions/login.md")]
    public void Create_SourceNotDirectlyInDiscussions_ThrowsInvalidFeatureSourceExceptionWithSourceNotMovable(
        FeatureSourceKind kind, string value)
    {
        // Arrange
        var source = new FeatureSource { Kind = kind, Value = value };

        // Act
        var act = () => DiscussionNotesMove.Create(source, "the source");

        // Assert
        var exception = act.Should().Throw<InvalidFeatureSourceException>().Which;

        exception.Problem
            .Should().Be(FeatureSourceProblem.SourceNotMovable);

        exception.SourceText
            .Should().Be("the source");

        exception.Reason
            .Should().Contain(".shadekit/discussions/");
    }

    [Theory]
    [InlineData("Login SSO")]
    [InlineData("https://example.com/requests/login-sso")]
    [InlineData("login\0.md")]
    [InlineData("missing.md")]
    public void EnsureFileSource_SourceThatNamesNoFile_ThrowsInvalidFeatureSourceExceptionWithSourceNotMovable(
        string source)
    {
        // Act
        var act = () => DiscussionNotesMove.EnsureFileSource(source, Path.GetTempPath());

        // Assert
        var exception = act.Should().Throw<InvalidFeatureSourceException>().Which;

        exception.Problem
            .Should().Be(FeatureSourceProblem.SourceNotMovable);

        exception.SourceText
            .Should().Be(source);
    }

    [Fact]
    public void EnsureFileSource_ExistingFile_DoesNotThrow()
    {
        // Arrange
        using var directory = new TempKitDirectory();
        directory.WriteFile("# Login SSO\n", ".shadekit", "discussions", "login.md");

        // Act
        var act = () => DiscussionNotesMove.EnsureFileSource(".shadekit/discussions/login.md", directory.RootPath);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_NullSource_ThrowsArgumentNullException()
    {
        // Act
        var act = () => DiscussionNotesMove.Create(null!, "notes.md");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }
}
