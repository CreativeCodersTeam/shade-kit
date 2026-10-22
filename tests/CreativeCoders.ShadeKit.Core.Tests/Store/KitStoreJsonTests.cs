using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Formats;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

public class KitStoreJsonTests
{
    private const string SampleFileName = "sample.json";

    private const string SampleDisplayPath = ".shadekit/sample.json";

    private const string ConfigDisplayPath = ".shadekit/config.json";

    [Fact]
    public async Task ReadJsonAsync_FileMissing_ReturnsNull()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().BeNull();
    }

    [Theory]
    [InlineData(FormatAccess.Read)]
    [InlineData(FormatAccess.Write)]
    public async Task ReadJsonAsync_CurrentVersion_ReturnsDocumentWithoutWarnings(FormatAccess access)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV2, ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, access,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Document.Title
            .Should().Be("Login");

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ReadJsonAsync_CurrentVersionWithInvalidConfig_DoesNotReadConfig()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV2, ".shadekit", SampleFileName);
        tempDirectory.WriteFile("not json", ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory);

        // Act
        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ReadJsonAsync_FileWithUtf8ByteOrderMark_ReadsDocument()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var path = tempDirectory.CreateDirectory(".shadekit");
        await File.WriteAllTextAsync(Path.Combine(path, SampleFileName), StoreTestFormats.SampleV2,
            new System.Text.UTF8Encoding(true), TestContext.Current.CancellationToken);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Document.Title
            .Should().Be("Login");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC10")]
    public async Task ReadJsonAsync_OlderVersionWithoutConfigFile_ReturnsMigratedDocumentWithWarning()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Document.Title
            .Should().Be("Login");

        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, SampleDisplayPath, 1, 2));
    }

    [Theory]
    [Trait("Requirement", "FP01-AC10")]
    [InlineData("migrate-and-warn")]
    [InlineData(null)]
    public async Task ReadJsonAsync_OlderVersionWithMigrateAndWarnOrMissingSetting_ReturnsMigratedDocumentWithWarning(
        string? outdatedFormatOnRead)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(1, outdatedFormatOnRead), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory);

        // Act
        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Document.Title
            .Should().Be("Login");

        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, SampleDisplayPath, 1, 2));
    }

    [Fact]
    [Trait("Requirement", "FP01-AC10")]
    public async Task ReadJsonAsync_OlderVersionWithStopSetting_ThrowsUpgradeNeededException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "stop"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<UpgradeNeededException>()).Which;

        exception.FilePath
            .Should().Be(SampleDisplayPath);

        exception.FileVersion
            .Should().Be(1);

        exception.CurrentVersion
            .Should().Be(2);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC5")]
    public async Task ReadJsonAsync_OlderVersionWithWriteAccess_ThrowsUpgradeNeededExceptionWhateverTheSetting()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "migrate-and-warn"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id,
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UpgradeNeededException>())
            .Which.FilePath.Should().Be(SampleDisplayPath);
    }

    [Fact]
    public async Task ReadJsonAsync_OlderVersionWithUpgradeAccess_MigratesWithWarningWithoutReadingConfig()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile("not json", ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory);

        // Act
        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id,
            FormatAccess.Upgrade, TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Document.Title
            .Should().Be("Login");

        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, SampleDisplayPath, 1, 2));
    }

    [Fact]
    public async Task ReadJsonAsync_OutdatedConfigWithStopAndUpgradeAccess_MigratesConfigWithWarning()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "stop"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        // Act
        var result = await kitStore.ReadJsonAsync<KitConfig>("config.json", FormatIds.Config, FormatAccess.Upgrade,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, ConfigDisplayPath, 1, 2));
    }

    [Theory]
    [InlineData((FormatAccess)42)]
    [InlineData(FormatAccess.Replace)]
    public async Task ReadJsonAsync_UndefinedOrReplaceAccess_ThrowsArgumentOutOfRangeException(FormatAccess access)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, access,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentOutOfRangeException>())
            .WithParameterName(nameof(access));
    }

    [Fact]
    [Trait("Requirement", "FP01-AC10")]
    public async Task ReadJsonAsync_OlderVersionAndOutdatedConfigWithStop_ThrowsUpgradeNeededExceptionForConfig()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "stop"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UpgradeNeededException>())
            .Which.FilePath.Should().Be(ConfigDisplayPath);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC10")]
    public async Task ReadJsonAsync_OlderVersionAndOutdatedConfigWithMigrateAndWarn_ReturnsWarningsForBothFiles()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "migrate-and-warn"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        // Act
        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, SampleDisplayPath, 1, 2),
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ConfigDisplayPath, 1, 2));
    }

    [Fact]
    public async Task ReadJsonAsync_OlderVersionAndConfigTooNew_ThrowsFormatTooNewExceptionForConfig()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(3, "migrate-and-warn"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<FormatTooNewException>()).Which;

        exception.FilePath
            .Should().Be(ConfigDisplayPath);

        exception.MinEngineVersion
            .Should().Be("0.3.0");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "minEngineVersion": "0.1.0" }""")]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "outdatedFormatOnRead": "sometimes" }""")]
    public async Task ReadJsonAsync_OlderVersionAndInvalidConfig_ThrowsInvalidKitFileExceptionForConfig(
        string configJson)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(configJson, ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>())
            .Which.FilePath.Should().Be(ConfigDisplayPath);
    }

    [Theory]
    [Trait("Requirement", "FP01-AC10")]
    [InlineData("migrate-and-warn")]
    [InlineData(null)]
    public async Task ReadJsonAsync_OutdatedConfigItselfWithMigrateAndWarn_ReturnsOneWarning(
        string? outdatedFormatOnRead)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.Config(1, outdatedFormatOnRead), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        // Act
        var result = await kitStore.ReadJsonAsync<KitConfig>("config.json", FormatIds.Config, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, ConfigDisplayPath, 1, 2));
    }

    [Fact]
    [Trait("Requirement", "FP01-AC10")]
    public async Task ReadJsonAsync_OutdatedConfigItselfWithStop_ThrowsUpgradeNeededException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "stop"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        // Act
        var act = () => kitStore.ReadJsonAsync<KitConfig>("config.json", FormatIds.Config, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UpgradeNeededException>())
            .Which.FilePath.Should().Be(ConfigDisplayPath);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC4")]
    public async Task ReadJsonAsync_NewerVersion_ThrowsFormatTooNewExceptionWithMinEngineVersion()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("""{ "schemaVersion": 3, "minEngineVersion": "2.0.0" }""", ".shadekit",
            SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<FormatTooNewException>()).Which;

        exception.FilePath
            .Should().Be(SampleDisplayPath);

        exception.MinEngineVersion
            .Should().Be("2.0.0");
    }

    [Theory]
    [Trait("Requirement", "FP01-AC11")]
    [InlineData(FormatAccess.Read)]
    [InlineData(FormatAccess.Write)]
    public async Task ReadJsonAsync_MissingSchemaVersion_ThrowsInvalidKitFileException(FormatAccess access)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("""{ "minEngineVersion": "0.2.0", "title": "Login" }""", ".shadekit",
            SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, access,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>())
            .Which.FilePath.Should().Be(SampleDisplayPath);
    }

    [Fact]
    public async Task ReadJsonAsync_FileInSubdirectory_ReportsPathRelativeToRepositoryRoot()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", ".shadekit", "features", "login", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(Path.Combine("features", "login", SampleFileName),
            SampleFormat.Id, FormatAccess.Read, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>())
            .Which.FilePath.Should().Be(".shadekit/features/login/sample.json");
    }

    [Theory]
    [InlineData(KitStorePaths.CurrentFeatureFilePath)]
    [InlineData("./features/current-feature.json")]
    [InlineData("features/./current-feature.json")]
    [InlineData("features/login/../current-feature.json")]
    public async Task ReadJsonAsync_InvalidFileAtOverridePath_ReportsDisplayPathOfKitStorePaths(string relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{}", ".shadekit", "features", KitStorePaths.CurrentFeatureFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(relativePath, SampleFormat.Id, FormatAccess.Write,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>())
            .Which.FilePath.Should().Be(KitStorePaths.GetDisplayPath(KitStorePaths.CurrentFeatureFilePath))
            .And.Be(".shadekit/features/current-feature.json");
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("features/../../outside.json")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ReadJsonAsync_PathOutsideKitRootOrEmpty_ThrowsArgumentException(string relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(relativePath, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReadJsonAsync_RootedPath_ThrowsArgumentException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(Path.Combine(tempDirectory.RootPath, "x.json"),
            SampleFormat.Id, FormatAccess.Read, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName("relativePath");
    }

    [Fact]
    public async Task ReadJsonAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV2, ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC9")]
    public async Task WriteJsonAsync_NewFileInMissingDirectory_WritesStampedUtf8FileWithoutByteOrderMark()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);
        var document = new SampleDocument { SchemaVersion = 1, MinEngineVersion = "9.9.9", Title = "Grüße" };

        // Act
        await kitStore.WriteJsonAsync(Path.Combine("features", "login", SampleFileName), SampleFormat.Id, document,
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        var bytes = tempDirectory.ReadFileBytes(".shadekit", "features", "login", SampleFileName);

        bytes.Take(3)
            .Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });

        var root = JsonNode.Parse(bytes);

        root
            .Should().NotBeNull();

        var schemaVersionNode = root["schemaVersion"];

        schemaVersionNode
            .Should().NotBeNull();

        schemaVersionNode.GetValue<int>()
            .Should().Be(2);

        var minEngineVersionNode = root["minEngineVersion"];

        minEngineVersionNode
            .Should().NotBeNull();

        minEngineVersionNode.GetValue<string>()
            .Should().Be("0.2.0");

        var titleNode = root["title"];

        titleNode
            .Should().NotBeNull();

        titleNode.GetValue<string>()
            .Should().Be("Grüße");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC3")]
    public async Task WriteJsonAsync_DocumentReadWithUnknownFields_KeepsEveryUnknownFieldAndValue()
    {
        // Arrange
        const string original =
            """
            {
              "schemaVersion": 2,
              "minEngineVersion": "0.2.0",
              "title": "Login",
              "mode": "default",
              "futureList": [1, { "deep": null }],
              "futureNumber": 1.50,
              "nested": { "name": "inner", "futureNested": true }
            }
            """;

        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(original, ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id,
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Act
        await kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, result!.Document,
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        var written = tempDirectory.ReadFileText(".shadekit", SampleFileName);

        JsonNode.DeepEquals(JsonNode.Parse(written), JsonNode.Parse(original))
            .Should().BeTrue($"the written file must keep every field, but was:{Environment.NewLine}{written}");

        written.Should().Contain("\"futureNumber\": 1.50");
    }

    [Fact]
    public async Task WriteJsonAsync_ExistingCurrentFile_ReplacesIt()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV2, ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        await kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument { Title = "Logout" },
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        var root = JsonNode.Parse(tempDirectory.ReadFileText(".shadekit", SampleFileName));

        root
            .Should().NotBeNull();

        var titleNode = root["title"];

        titleNode
            .Should().NotBeNull();

        titleNode.GetValue<string>()
            .Should().Be("Logout");
    }

    [Fact]
    [Trait("Requirement", "FP01-AC5")]
    public async Task WriteJsonAsync_ExistingOlderFile_ThrowsUpgradeNeededExceptionAndLeavesFileByteForByteUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "migrate-and-warn"), ".shadekit", "config.json");
        var originalBytes = tempDirectory.ReadFileBytes(".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument { Title = "New" },
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UpgradeNeededException>())
            .Which.FilePath.Should().Be(SampleDisplayPath);

        tempDirectory.ReadFileBytes(".shadekit", SampleFileName)
            .Should().Equal(originalBytes);
    }

    [Fact]
    public async Task WriteJsonAsync_DocumentReadWithUpgradeAccess_ReplacesOlderFileWithCurrentVersion()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "stop"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory);

        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id,
            FormatAccess.Upgrade, TestContext.Current.CancellationToken);

        // Act
        await kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, result!.Document, FormatAccess.Upgrade,
            TestContext.Current.CancellationToken);

        // Assert
        var root = JsonNode.Parse(tempDirectory.ReadFileText(".shadekit", SampleFileName));

        root
            .Should().NotBeNull();

        var schemaVersionNode = root["schemaVersion"];

        schemaVersionNode
            .Should().NotBeNull();

        schemaVersionNode.GetValue<int>()
            .Should().Be(2);

        var titleNode = root["title"];

        titleNode
            .Should().NotBeNull();

        titleNode.GetValue<string>()
            .Should().Be("Login");

        var rereadResult = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id,
            FormatAccess.Write, TestContext.Current.CancellationToken);

        rereadResult
            .Should().NotBeNull();

        rereadResult.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC4")]
    public async Task WriteJsonAsync_ExistingNewerFileWithUpgradeAccess_ThrowsFormatTooNewExceptionAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("""{ "schemaVersion": 3, "minEngineVersion": "2.0.0" }""", ".shadekit",
            SampleFileName);
        var originalBytes = tempDirectory.ReadFileBytes(".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument(),
            FormatAccess.Upgrade, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<FormatTooNewException>();

        tempDirectory.ReadFileBytes(".shadekit", SampleFileName)
            .Should().Equal(originalBytes);
    }

    [Theory]
    [InlineData(FormatAccess.Read)]
    [InlineData((FormatAccess)42)]
    public async Task WriteJsonAsync_ReadOrUndefinedAccess_ThrowsArgumentOutOfRangeExceptionAndWritesNothing(
        FormatAccess access)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync(Path.Combine("features", "login", SampleFileName), SampleFormat.Id,
            new SampleDocument(), access, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentOutOfRangeException>())
            .WithParameterName(nameof(access));

        Directory.Exists(Path.Combine(kitStore.RootPath, "features"))
            .Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC4")]
    public async Task WriteJsonAsync_ExistingNewerFile_ThrowsFormatTooNewExceptionAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("""{ "schemaVersion": 3, "minEngineVersion": "2.0.0" }""", ".shadekit",
            SampleFileName);
        var originalBytes = tempDirectory.ReadFileBytes(".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument(),
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<FormatTooNewException>();

        tempDirectory.ReadFileBytes(".shadekit", SampleFileName)
            .Should().Equal(originalBytes);
    }

    [Fact]
    [Trait("Requirement", "FP01-AC11")]
    public async Task WriteJsonAsync_ExistingFileWithoutSchemaVersion_ThrowsInvalidKitFileExceptionAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("""{ "title": "hand-edited" }""", ".shadekit", SampleFileName);
        var originalBytes = tempDirectory.ReadFileBytes(".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument(),
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidKitFileException>();

        tempDirectory.ReadFileBytes(".shadekit", SampleFileName)
            .Should().Equal(originalBytes);
    }

    [Fact]
    public async Task WriteJsonAsync_NullDocument_ThrowsArgumentNullException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, null!,
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("document");
    }

    [Fact]
    public async Task WriteJsonAsync_PathOutsideKitRoot_ThrowsArgumentExceptionAndWritesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync("../outside.json", SampleFormat.Id, new SampleDocument(),
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        File.Exists(Path.Combine(tempDirectory.RootPath, "outside.json"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task WriteJsonAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument(),
            FormatAccess.Write, new CancellationToken(true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadJsonAsync_UnknownFormatIdAndMissingFile_ThrowsArgumentException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, "unknown", FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName("formatId");
    }

    [Fact]
    public async Task WriteJsonAsync_UnknownFormatId_ThrowsArgumentExceptionAndCreatesNothing()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync(Path.Combine("features", "login", SampleFileName), "unknown",
            new SampleDocument(), FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName("formatId");

        Directory.Exists(Path.Combine(kitStore.RootPath, "features"))
            .Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FP01-AC10")]
    public async Task ReadJsonAsync_OlderVersionWithMigrateAndWarn_LeavesFilesOnDiskUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "migrate-and-warn"), ".shadekit", "config.json");
        var originalSample = tempDirectory.ReadFileBytes(".shadekit", SampleFileName);
        var originalConfig = tempDirectory.ReadFileBytes(".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        // Act
        await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(".shadekit", SampleFileName)
            .Should().Equal(originalSample);

        tempDirectory.ReadFileBytes(".shadekit", "config.json")
            .Should().Equal(originalConfig);
    }

    [Fact]
    public async Task ReadJsonAsync_OlderVersionWithWriteAccessAndInvalidConfig_ThrowsUpgradeNeededExceptionForTarget()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile("not json", ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id,
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UpgradeNeededException>())
            .Which.FilePath.Should().Be(SampleDisplayPath);
    }

    [Fact]
    public async Task ReadJsonAsync_CurrentVersionAndOutdatedConfigWithStop_ReturnsResultWithoutWarnings()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV2, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "stop"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        // Act
        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ReadJsonAsync_OutdatedConfigItselfWithWriteAccess_ThrowsUpgradeNeededException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "migrate-and-warn"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        // Act
        var act = () => kitStore.ReadJsonAsync<KitConfig>("config.json", FormatIds.Config,
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UpgradeNeededException>();
    }

    [Theory]
    [InlineData("./config.json")]
    [InlineData("features/../config.json")]
    public async Task ReadJsonAsync_OutdatedConfigItselfByOtherSpelling_ReturnsOneWarning(string relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "migrate-and-warn"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        // Act
        var result = await kitStore.ReadJsonAsync<KitConfig>(relativePath, FormatIds.Config, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, ConfigDisplayPath, 1, 2));
    }

    [Fact]
    public async Task ReadJsonAsync_OutdatedConfigItselfByOtherCaseOnCaseInsensitiveFileSystem_ReturnsOneWarning()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "migrate-and-warn"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        Assert.SkipUnless(File.Exists(Path.Combine(kitStore.RootPath, "CONFIG.json")),
            "The file system of the temporary directory is case-sensitive.");

        // Act
        var result = await kitStore.ReadJsonAsync<KitConfig>("CONFIG.json", FormatIds.Config, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Warnings
            .Should().ContainSingle();
    }

    [Theory]
    [InlineData(FormatAccess.Read)]
    [InlineData(FormatAccess.Write)]
    public async Task ReadJsonAsync_EmptyFile_ThrowsInvalidKitFileException(FormatAccess access)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(string.Empty, ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, access,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidKitFileException>();
    }

    [Fact]
    public async Task WriteJsonAsync_ExistingTruncatedFile_ThrowsInvalidKitFileExceptionAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{ \"schemaVersion\": 2, \"minEngi", ".shadekit", SampleFileName);
        var originalBytes = tempDirectory.ReadFileBytes(".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument(),
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidKitFileException>();

        tempDirectory.ReadFileBytes(".shadekit", SampleFileName)
            .Should().Equal(originalBytes);
    }

    [Theory]
    [InlineData("../.shadekit-evil/x.json")]
    [InlineData(".")]
    [InlineData("features/..")]
    public async Task ReadJsonAsync_PathEscapingOrEqualToKitRoot_ThrowsArgumentException(string path)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(path, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName("relativePath");
    }

    [Theory]
    [InlineData("../.shadekit-evil/x.json")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task WriteJsonAsync_InvalidRelativePath_ThrowsArgumentExceptionAndWritesNothing(string? relativePath)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync(relativePath!, SampleFormat.Id, new SampleDocument(),
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        Directory.Exists(Path.Combine(tempDirectory.RootPath, ".shadekit-evil"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task ReadJsonAsync_NullRelativePath_ThrowsArgumentException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(null!, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WriteJsonAsync_ExistingLongerFile_LeavesNoTrailingBytes()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var longTitle = new string('x', 2000);
        tempDirectory.WriteFile(
            $$"""{ "schemaVersion": 2, "minEngineVersion": "0.2.0", "title": "{{longTitle}}" }""",
            ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);
        var document = new SampleDocument { Title = "short" };

        // Act
        await kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, document,
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileText(".shadekit", SampleFileName)
            .Should().Be(new VersionedJsonSerializer(StoreTestFormats.CreateRegistry())
                .Serialize(document, SampleFormat.Id));
    }

    [Fact]
    public async Task WriteJsonAsync_ExistingFileWithByteOrderMark_RewritesItWithoutByteOrderMark()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var path = tempDirectory.CreateDirectory(".shadekit");
        await File.WriteAllTextAsync(Path.Combine(path, SampleFileName), StoreTestFormats.SampleV2,
            new System.Text.UTF8Encoding(true), TestContext.Current.CancellationToken);
        var kitStore = CreateStore(tempDirectory);

        // Act
        await kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument { Title = "Login" },
            FormatAccess.Write, TestContext.Current.CancellationToken);

        // Assert
        tempDirectory.ReadFileBytes(".shadekit", SampleFileName).Take(3)
            .Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
    }

    [Fact]
    public async Task ReadJsonAsync_SecondOlderFileAfterConfigChangedOnDisk_ReusesFirstConfigRead()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", "other.json");
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "migrate-and-warn"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // A second read of config.json would fail.
        tempDirectory.WriteFile("not json", ".shadekit", "config.json");

        // Act
        var result = await kitStore.ReadJsonAsync<SampleDocument>("other.json", SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ".shadekit/other.json", 1, 2),
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ConfigDisplayPath, 1, 2));
    }

    [Fact]
    public async Task ReadJsonAsync_ConfigReadFailedBefore_ReadsConfigAgain()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile("not json", ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory, configVersion: 2);

        var firstRead = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id,
            FormatAccess.Read, TestContext.Current.CancellationToken);

        await firstRead.Should().ThrowAsync<InvalidKitFileException>();

        tempDirectory.WriteFile(StoreTestFormats.Config(1, "migrate-and-warn"), ".shadekit", "config.json");

        // Act
        var result = await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().NotBeNull();

        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, SampleDisplayPath, 1, 2),
                new FormatWarning(FormatWarningKind.UpgradeNeeded, ConfigDisplayPath, 1, 2));
    }

    [Fact]
    public async Task ReadJsonAsync_ConfigWrittenThroughStoreAfterRead_UsesNewSetting()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "migrate-and-warn"), ".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory);

        await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        await kitStore.WriteFileAsync("config.json", Encoding.UTF8.GetBytes(StoreTestFormats.Config(1, "stop")),
            TestContext.Current.CancellationToken);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UpgradeNeededException>())
            .Which.FilePath.Should().Be(SampleDisplayPath);
    }

    [Theory]
    [InlineData("delete-file")]
    [InlineData("delete-directory")]
    [InlineData("move-directory")]
    [InlineData("create-directory")]
    public async Task ReadJsonAsync_StoreChangedAfterRead_ReadsConfigAgain(string change)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(StoreTestFormats.Config(1, "migrate-and-warn"), ".shadekit", "config.json");
        tempDirectory.WriteFile("{}", ".shadekit", "scratch", "file.json");
        var kitStore = CreateStore(tempDirectory);

        await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        tempDirectory.WriteFile(StoreTestFormats.Config(1, "stop"), ".shadekit", "config.json");

        switch (change)
        {
            case "delete-file":
                kitStore.DeleteFile("scratch/file.json");
                break;
            case "delete-directory":
                kitStore.DeleteDirectory("scratch");
                break;
            case "move-directory":
                kitStore.MoveDirectory("scratch", "moved");
                break;
            default:
                kitStore.CreateDirectory("created");
                break;
        }

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Read,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UpgradeNeededException>())
            .Which.FilePath.Should().Be(SampleDisplayPath);
    }

    [Fact]
    public async Task WriteJsonAsync_ReplaceAccessAndFileMissing_WritesFileInMissingDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var kitStore = CreateStore(tempDirectory);
        var relativePath = Path.Combine("features", "login", SampleFileName);

        // Act
        await kitStore.WriteJsonAsync(relativePath, SampleFormat.Id, new SampleDocument { Title = "Logout" },
            FormatAccess.Replace, TestContext.Current.CancellationToken);

        // Assert
        (await ReadTitleAsync(kitStore, relativePath))
            .Should().Be("Logout");
    }

    [Theory]
    [InlineData(StoreTestFormats.SampleV2)]
    [InlineData(StoreTestFormats.SampleV1)]
    [InlineData("""{ "schemaVersion": 3, "minEngineVersion": "2.0.0" }""")]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("""{ "title": "Login" }""")]
    [InlineData("""{ "schemaVersion": 2, "title": "Login" }""")]
    [InlineData("""{ "schemaVersion": 2, "minEngineVersion": "0.2.0", "title": 5 }""")]
    public async Task WriteJsonAsync_ReplaceAccessAndExistingFile_ReplacesItWithCurrentVersion(string existingContent)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(existingContent, ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        await kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument { Title = "Logout" },
            FormatAccess.Replace, TestContext.Current.CancellationToken);

        // Assert
        (await ReadTitleAsync(kitStore, SampleFileName))
            .Should().Be("Logout", "the file is read back with write access, so it is in the current version");
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", "outdatedFormatOnRead": "stop" }""")]
    [InlineData("{")]
    [InlineData("""{ "schemaVersion": 99, "minEngineVersion": "99.0.0" }""")]
    public async Task WriteJsonAsync_ReplaceAccessAndOlderFileWithStopOrUnusableConfig_ReplacesFileAndKeepsConfig(
        string configContent)
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        tempDirectory.WriteFile(configContent, ".shadekit", "config.json");
        var originalConfigBytes = tempDirectory.ReadFileBytes(".shadekit", "config.json");
        var kitStore = CreateStore(tempDirectory);

        // Act
        await kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument { Title = "Logout" },
            FormatAccess.Replace, TestContext.Current.CancellationToken);

        // Assert
        (await ReadTitleAsync(kitStore, SampleFileName))
            .Should().Be("Logout");

        tempDirectory.ReadFileBytes(".shadekit", "config.json")
            .Should().Equal(originalConfigBytes);
    }

    [Fact]
    public async Task WriteJsonAsync_ReplaceAccessAndDirectoryAtPath_ThrowsAndKeepsDirectory()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        var directoryPath = tempDirectory.CreateDirectory(".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument(),
            FormatAccess.Replace, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<Exception>())
            .Which
            .Should().Match<Exception>(x => x is IOException || x is UnauthorizedAccessException);

        Directory.Exists(directoryPath)
            .Should().BeTrue();
    }

    [Fact]
    public async Task WriteJsonAsync_ReplaceAccessAndUnknownFormatIdWithExistingFile_ThrowsAndLeavesFileUnchanged()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile("{", ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.WriteJsonAsync(SampleFileName, "unknown", new SampleDocument(),
            FormatAccess.Replace, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentException>())
            .WithParameterName("formatId");

        tempDirectory.ReadFileText(".shadekit", SampleFileName)
            .Should().Be("{");
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task WriteJsonAsync_ReplaceAccessAndUnreadableFile_ThrowsUnauthorizedAccessExceptionAndKeepsFile()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file unreadable.");
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("{", ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);
        File.SetUnixFileMode(filePath, UnixFileMode.UserWrite);

        try
        {
            Assert.SkipWhen(CanRead(filePath),
                $"'{filePath}' is readable without permissions, for example because the tests run as root.");

            // Act
            var act = () => kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument(),
                FormatAccess.Replace, TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        finally
        {
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        tempDirectory.ReadFileText(".shadekit", SampleFileName)
            .Should().Be("{");
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task WriteJsonAsync_ReplaceAccessAndReadOnlyUnusableFile_DeletesItAndWritesNewFile()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file read-only.");
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile("{", ".shadekit", SampleFileName);
        File.SetUnixFileMode(filePath, UnixFileMode.UserRead);
        var kitStore = CreateStore(tempDirectory);

        // Act
        await kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument { Title = "Logout" },
            FormatAccess.Replace, TestContext.Current.CancellationToken);

        // Assert
        (await ReadTitleAsync(kitStore, SampleFileName))
            .Should().Be("Logout");
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task WriteJsonAsync_ReplaceAccessAndReadOnlyCurrentFile_ThrowsUnauthorizedAccessExceptionAndKeepsFile()
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes are needed to make a file read-only.");
        using var tempDirectory = new TempKitDirectory();
        var filePath = tempDirectory.WriteFile(StoreTestFormats.SampleV2, ".shadekit", SampleFileName);
        File.SetUnixFileMode(filePath, UnixFileMode.UserRead);
        var kitStore = CreateStore(tempDirectory);

        try
        {
            Assert.SkipWhen(CanWrite(filePath),
                $"'{filePath}' is writable without permissions, for example because the tests run as root.");

            // Act
            var act = () => kitStore.WriteJsonAsync(SampleFileName, SampleFormat.Id, new SampleDocument(),
                FormatAccess.Replace, TestContext.Current.CancellationToken);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>(
                "a usable file is overwritten in place, not deleted");
        }
        finally
        {
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        tempDirectory.ReadFileText(".shadekit", SampleFileName)
            .Should().Be(StoreTestFormats.SampleV2);
    }

    private static async Task<string?> ReadTitleAsync(KitStore kitStore, string relativePath)
    {
        var result = await kitStore.ReadJsonAsync<SampleDocument>(relativePath, SampleFormat.Id, FormatAccess.Write,
            TestContext.Current.CancellationToken);

        result
            .Should().NotBeNull();

        result.Warnings
            .Should().BeEmpty();

        return result.Document.Title;
    }

    private static bool CanRead(string filePath)
    {
        try
        {
            _ = File.ReadAllBytes(filePath);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool CanWrite(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Write);

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static KitStore CreateStore(TempKitDirectory tempDirectory, int configVersion = 1)
    {
        var kitStorePath = tempDirectory.CreateDirectory(".shadekit");
        var formatRegistry = StoreTestFormats.CreateRegistry(configVersion: configVersion);

        return new KitStore(kitStorePath, new VersionedJsonSerializer(formatRegistry), formatRegistry);
    }
}
