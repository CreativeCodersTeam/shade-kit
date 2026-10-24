using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Files;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public class KitStoreFeatureExtensionsTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void FeatureExists_ValidId_ReturnsWhetherFeatureFolderExists(bool folderExists, bool expected)
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.GetFeatureNames())
            .Returns(folderExists ? ["gh-7-checkout", "gh-142-login-sso"] : ["gh-7-checkout"]);

        // Act
        var exists = store.FeatureExists("gh-142-login-sso");

        // Assert
        exists
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("GH-142-LOGIN-SSO")]
    [InlineData("Gh-142-login-sso")]
    [InlineData("gh-142-login-SSO")]
    public void FeatureExists_FolderNameDiffersOnlyInCase_ReturnsFalse(string folderName)
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.GetFeatureNames()).Returns([folderName]);
        A.CallTo(() => store.DirectoryExists(A<string>._)).Returns(true);

        // Act
        var exists = store.FeatureExists("gh-142-login-sso");

        // Assert
        exists
            .Should().BeFalse();
    }

    [Fact]
    public void FeatureExists_ExistingAndCaseVariantFolders_ReturnsTrue()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.GetFeatureNames()).Returns(["GH-142-LOGIN-SSO", "gh-142-login-sso"]);

        // Act
        var exists = store.FeatureExists("gh-142-login-sso");

        // Assert
        exists
            .Should().BeTrue();
    }

    [Fact]
    public void FeatureExists_RealFolderNameDiffersOnlyInCase_ReturnsFalse()
    {
        // Arrange
        using var repository = new TempKitDirectory();
        var kitStorePath = repository.CreateDirectory(".shadekit");
        repository.CreateDirectory(".shadekit", "features", "GH-142-Login-SSO");
        var registry = FormatRegistry.CreateDefault();
        var store = new KitStore(kitStorePath, new VersionedJsonSerializer(registry), registry,
            FixedLineEnding.CreateWriter(LineEnding.Lf));

        // Act
        var exists = store.FeatureExists("gh-142-login-sso");

        // Assert
        exists
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("../gh-142")]
    [InlineData("Login")]
    [InlineData(".gh-142.1a2b3c4d.tmp")]
    public void FeatureExists_InvalidId_ReturnsFalseWithoutFileSystemAccess(string featureId)
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.GetFeatureNames()).Returns([featureId]);
        A.CallTo(() => store.DirectoryExists(A<string>._)).Returns(true);

        // Act
        var exists = store.FeatureExists(featureId);

        // Assert
        exists
            .Should().BeFalse();

        A.CallTo(store)
            .MustNotHaveHappened();
    }

    [Fact]
    public void FeatureExists_NullStore_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ((IKitStore)null!).FeatureExists("gh-142-login-sso");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("store");
    }
}
