using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Init;

namespace CreativeCoders.ShadeKit.Core.Tests.Formats;

public class JsonEnumNamesTests
{
    [Theory]
    [InlineData(FeatureSourceKind.Text, "text")]
    [InlineData(FeatureSourceKind.Url, "url")]
    [InlineData(FeatureSourceKind.File, "file")]
    public void ToJsonName_SingleWordMember_ReturnsLowerCaseName(FeatureSourceKind kind, string expectedName)
    {
        // Act
        var name = kind.ToJsonName();

        // Assert
        name
            .Should().Be(expectedName);
    }

    [Theory]
    [InlineData(InitializedFileAction.Created, "created")]
    [InlineData(InitializedFileAction.Appended, "appended")]
    [InlineData(InitializedFileAction.Updated, "updated")]
    public void ToJsonName_FileAction_ReturnsLowerCaseName(InitializedFileAction action, string expectedName)
    {
        // Act
        var name = action.ToJsonName();

        // Assert
        name
            .Should().Be(expectedName);
    }

    [Fact]
    public void ToJsonName_MultiWordMember_ReturnsKebabCaseName()
    {
        // Act
        var name = FormatWarningKind.UpgradeNeeded.ToJsonName();

        // Assert
        name
            .Should().Be("upgrade-needed");
    }
}
