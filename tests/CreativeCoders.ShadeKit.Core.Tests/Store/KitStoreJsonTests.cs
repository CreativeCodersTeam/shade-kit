using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Formats;

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
        result!.Document.Title
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
        result!.Warnings
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
        result!.Document.Title
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
        result!.Document.Title
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
        result!.Document.Title
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
        result!.Document.Title
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
        result!.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, ConfigDisplayPath, 1, 2));
    }

    [Fact]
    public async Task ReadJsonAsync_UndefinedAccess_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var tempDirectory = new TempKitDirectory();
        tempDirectory.WriteFile(StoreTestFormats.SampleV1, ".shadekit", SampleFileName);
        var kitStore = CreateStore(tempDirectory);

        // Act
        var act = () => kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, (FormatAccess)42,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentOutOfRangeException>())
            .WithParameterName("access");
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
        result!.Warnings
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
        result!.Warnings
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

        var root = JsonNode.Parse(bytes)!;

        root["schemaVersion"]!.GetValue<int>()
            .Should().Be(2);

        root["minEngineVersion"]!.GetValue<string>()
            .Should().Be("0.2.0");

        root["title"]!.GetValue<string>()
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
        JsonNode.Parse(tempDirectory.ReadFileText(".shadekit", SampleFileName))!["title"]!.GetValue<string>()
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
        var root = JsonNode.Parse(tempDirectory.ReadFileText(".shadekit", SampleFileName))!;

        root["schemaVersion"]!.GetValue<int>()
            .Should().Be(2);

        root["title"]!.GetValue<string>()
            .Should().Be("Login");

        (await kitStore.ReadJsonAsync<SampleDocument>(SampleFileName, SampleFormat.Id, FormatAccess.Write,
                TestContext.Current.CancellationToken))!.Warnings
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
    public async Task WriteJsonAsync_AccessOtherThanWriteOrUpgrade_ThrowsArgumentOutOfRangeExceptionAndWritesNothing(
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
        result!.Warnings
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
        result!.Warnings
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
        result!.Warnings
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

    private static KitStore CreateStore(TempKitDirectory tempDirectory, int configVersion = 1)
    {
        var kitStorePath = tempDirectory.CreateDirectory(".shadekit");
        var formatRegistry = StoreTestFormats.CreateRegistry(configVersion: configVersion);

        return new KitStore(kitStorePath, new VersionedJsonSerializer(formatRegistry), formatRegistry);
    }
}
