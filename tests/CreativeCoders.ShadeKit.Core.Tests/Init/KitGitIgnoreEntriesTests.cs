using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Init;

namespace CreativeCoders.ShadeKit.Core.Tests.Init;

public class KitGitIgnoreEntriesTests
{
    [Fact]
    [Trait("Requirement", "FP02-AC4")]
    public void All_Always_ContainsLocalOnlyFilesWithoutLeadingSlash()
    {
        // Act
        var entries = KitGitIgnoreEntries.All;

        // Assert
        entries
            .Should().Equal(".shadekit/local.json", ".shadekit/features/current-feature.json");
    }
}
