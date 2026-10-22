using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Store;
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
        A.CallTo(() => store.DirectoryExists("features/gh-142-login-sso")).Returns(folderExists);

        // Act
        var exists = store.FeatureExists("gh-142-login-sso");

        // Assert
        exists
            .Should().Be(expected);
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
        A.CallTo(() => store.DirectoryExists(A<string>._)).Returns(true);

        // Act
        var exists = store.FeatureExists(featureId);

        // Assert
        exists
            .Should().BeFalse();

        A.CallTo(() => store.DirectoryExists(A<string>._))
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
