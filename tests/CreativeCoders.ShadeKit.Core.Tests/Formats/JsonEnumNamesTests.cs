using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Tests.Formats;

public class JsonEnumNamesTests
{
    [Theory]
    [InlineData(FeatureSourceKind.Text, "text")]
    [InlineData(FeatureSourceKind.Url, "url")]
    [InlineData(FeatureSourceKind.File, "file")]
    public void Get_SingleWordMember_ReturnsLowerCaseName(FeatureSourceKind kind, string expectedName)
    {
        // Act
        var name = JsonEnumNames.Get(kind);

        // Assert
        name
            .Should().Be(expectedName);
    }

    [Fact]
    public void Get_MultiWordMember_ReturnsKebabCaseName()
    {
        // Act
        var name = JsonEnumNames.Get(FormatWarningKind.UpgradeNeeded);

        // Assert
        name
            .Should().Be("upgrade-needed");
    }
}
