using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Tests.Formats;

public class FormatRegistryTests
{
    [Fact]
    [Trait("Requirement", "FP01-AC7")]
    public void CreateDefault_Called_RegistersEveryFileTypeAtVersionOneInContractOrder()
    {
        // Act
        var registry = FormatRegistry.CreateDefault();

        // Assert
        registry.Formats.Select(x => (x.Id, x.CurrentVersion))
            .Should().Equal(
                (FormatIds.Config, 1),
                (FormatIds.Gates, 1),
                (FormatIds.Feature, 1),
                (FormatIds.CurrentFeature, 1),
                (FormatIds.TemplatesLock, 1),
                (FormatIds.Integrations, 1));
    }

    [Fact]
    [Trait("Requirement", "FP01-AC9")]
    public void CreateDefault_Called_MapsFormatVersionOneToEngineVersion010()
    {
        // Act
        var registry = FormatRegistry.CreateDefault();

        // Assert
        registry.Formats.Select(x => x.GetMinEngineVersion(1))
            .Should().AllBe("0.1.0");
    }

    [Fact]
    public void CreateDefault_Called_UsesContractSpellingOfFormatIds()
    {
        // Act
        var ids = FormatRegistry.CreateDefault().Formats.Select(x => x.Id);

        // Assert
        ids.Should().Equal("config", "gates", "feature", "current-feature", "templates-lock", "integrations");
    }

    [Fact]
    public void GetFormat_RegisteredId_ReturnsDefinition()
    {
        // Arrange
        var registry = FormatRegistry.CreateDefault();

        // Act
        var format = registry.GetFormat(FormatIds.Feature);

        // Assert
        format.Id
            .Should().Be(FormatIds.Feature);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("Config")]
    [InlineData("")]
    public void GetFormat_UnregisteredId_ThrowsArgumentException(string id)
    {
        // Arrange
        var registry = FormatRegistry.CreateDefault();

        // Act
        var act = () => registry.GetFormat(id);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("formatId");
    }

    [Fact]
    public void GetFormat_NullId_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = FormatRegistry.CreateDefault();

        // Act
        var act = () => registry.GetFormat(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formatId");
    }

    [Fact]
    public void Ctor_DuplicateId_ThrowsArgumentException()
    {
        // Arrange
        var table = new Dictionary<int, string> { [1] = "0.1.0" };

        // Act
        var act = () => new FormatRegistry(
            [new FormatDefinition(SampleFormat.Id, table), new FormatDefinition(SampleFormat.Id, table)]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("formats");
    }

    [Fact]
    public void Ctor_NoFormats_CreatesEmptyRegistry()
    {
        // Act
        var registry = new FormatRegistry([]);

        // Assert
        registry.Formats
            .Should().BeEmpty();

        registry.Invoking(x => x.GetFormat(FormatIds.Config))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NullFormats_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FormatRegistry(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("formats");
    }

    [Fact]
    public void Ctor_NullDefinitionInList_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FormatRegistry([null!]);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
