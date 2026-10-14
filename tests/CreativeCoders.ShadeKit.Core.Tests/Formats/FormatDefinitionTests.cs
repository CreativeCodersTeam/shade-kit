using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Tests.Formats;

public class FormatDefinitionTests
{
    [Fact]
    public void Ctor_TableWithThreeVersions_SetsIdAndCurrentVersion()
    {
        // Act
        var format = CreateSampleDefinition();

        // Assert
        format.Id
            .Should().Be(SampleFormat.Id);

        format.CurrentVersion
            .Should().Be(3);
    }

    [Theory]
    [InlineData(1, "0.1.0")]
    [InlineData(2, "0.2.0")]
    [InlineData(3, "0.3.0")]
    public void GetMinEngineVersion_KnownVersion_ReturnsEngineVersionFromTable(int formatVersion,
        string expectedMinEngineVersion)
    {
        // Arrange
        var format = CreateSampleDefinition();

        // Act
        var minEngineVersion = format.GetMinEngineVersion(formatVersion);

        // Assert
        minEngineVersion
            .Should().Be(expectedMinEngineVersion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void GetMinEngineVersion_VersionOutsideTable_ThrowsArgumentOutOfRangeException(int version)
    {
        // Arrange
        var format = CreateSampleDefinition();

        // Act
        var act = () => format.GetMinEngineVersion(version);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("formatVersion");
    }

    [Theory]
    [InlineData(1, new[] { 1, 2 })]
    [InlineData(2, new[] { 2 })]
    [InlineData(3, new int[0])]
    public void GetMigrations_FromVersion_ReturnsStepsUpToCurrentVersionInOrder(int fromVersion,
        int[] expectedFromVersions)
    {
        // Arrange
        var format = CreateSampleDefinition();

        // Act
        var migrations = format.GetMigrations(fromVersion);

        // Assert
        migrations.Select(x => x.FromVersion)
            .Should().Equal(expectedFromVersions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void GetMigrations_VersionOutsideTable_ThrowsArgumentOutOfRangeException(int startVersion)
    {
        // Arrange
        var format = CreateSampleDefinition();

        // Act
        var act = () => format.GetMigrations(startVersion);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fromVersion");
    }

    [Fact]
    public void Ctor_MigrationsInReverseOrder_ReturnsStepsInVersionOrder()
    {
        // Arrange
        IFormatMigration[] migrations = [new SampleFormat.AddModeMigration(), new SampleFormat.RenameNameToTitleMigration()];

        // Act
        var format = new FormatDefinition(SampleFormat.Id, SampleFormat.CreateMinEngineVersions(3), migrations);

        // Assert
        format.GetMigrations(1).Select(x => x.FromVersion)
            .Should().Equal(1, 2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Ctor_IdNullOrWhitespace_ThrowsArgumentException(string? id)
    {
        // Act
        var act = () => new FormatDefinition(id!, SampleFormat.CreateMinEngineVersions(1));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NullTable_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FormatDefinition(SampleFormat.Id, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("minEngineVersions");
    }

    [Fact]
    public void Ctor_EmptyTable_ThrowsArgumentException()
    {
        // Act
        var act = () => new FormatDefinition(SampleFormat.Id, new Dictionary<int, string>());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("minEngineVersions");
    }

    [Fact]
    public void Ctor_TableWithGap_ThrowsArgumentException()
    {
        // Arrange
        var table = new Dictionary<int, string> { [1] = "0.1.0", [3] = "0.3.0" };

        // Act
        var act = () => new FormatDefinition(SampleFormat.Id, table);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("minEngineVersions");
    }

    [Fact]
    public void Ctor_TableNotStartingAtOne_ThrowsArgumentException()
    {
        // Arrange
        var table = new Dictionary<int, string> { [2] = "0.2.0" };

        // Act
        var act = () => new FormatDefinition(SampleFormat.Id, table);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("minEngineVersions");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Ctor_TableWithEmptyEngineVersion_ThrowsArgumentException(string? minEngineVersion)
    {
        // Arrange
        var table = new Dictionary<int, string> { [1] = minEngineVersion! };

        // Act
        var act = () => new FormatDefinition(SampleFormat.Id, table);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("minEngineVersions");
    }

    [Fact]
    public void Ctor_MigrationOfOtherFileType_ThrowsArgumentException()
    {
        // Arrange
        var migration = new NoOpMigration("other", 1);

        // Act
        var act = () => new FormatDefinition(SampleFormat.Id, SampleFormat.CreateMinEngineVersions(2), [migration]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("migrations");
    }

    [Fact]
    public void Ctor_DuplicateMigration_ThrowsArgumentException()
    {
        // Arrange
        IFormatMigration[] migrations = [new NoOpMigration(SampleFormat.Id, 1), new NoOpMigration(SampleFormat.Id, 1)];

        // Act
        var act = () => new FormatDefinition(SampleFormat.Id, SampleFormat.CreateMinEngineVersions(3), migrations);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("migrations");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Ctor_MigrationOutsideVersionRange_ThrowsArgumentException(int fromVersion)
    {
        // Arrange
        var migration = new NoOpMigration(SampleFormat.Id, fromVersion);

        // Act
        var act = () => new FormatDefinition(SampleFormat.Id, SampleFormat.CreateMinEngineVersions(2), [migration]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("migrations");
    }

    [Fact]
    public void Ctor_MissingMigration_ThrowsArgumentException()
    {
        // Act
        var act = () => new FormatDefinition(SampleFormat.Id, SampleFormat.CreateMinEngineVersions(3),
            [new SampleFormat.RenameNameToTitleMigration()]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("migrations");
    }

    [Fact]
    public void Ctor_NullMigrationInList_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FormatDefinition(SampleFormat.Id, SampleFormat.CreateMinEngineVersions(2), [null!]);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_TableChangedAfterConstruction_KeepsOriginalTable()
    {
        // Arrange
        var table = SampleFormat.CreateMinEngineVersions(1);
        var format = new FormatDefinition(SampleFormat.Id, table);

        // Act
        table[1] = "9.9.9";

        // Assert
        format.GetMinEngineVersion(1)
            .Should().Be("0.1.0");
    }

    private static FormatDefinition CreateSampleDefinition()
    {
        return SampleFormat.CreateRegistry(3).GetFormat(SampleFormat.Id);
    }
}
