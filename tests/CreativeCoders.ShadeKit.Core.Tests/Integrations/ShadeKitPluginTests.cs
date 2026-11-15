using System.Text.Json.Nodes;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Tests.Skills;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations;

// Guards the constants against the manifests that every agent reads.
public class ShadeKitPluginTests
{
    [Fact]
    public void MarketplaceName_Always_EqualsNameInMarketplaceManifest()
    {
        // Arrange
        var manifest = ReadManifest("marketplace.json");

        // Act
        var name = manifest["name"]?.GetValue<string>();

        // Assert
        name
            .Should().Be(ShadeKitPlugin.MarketplaceName);
    }

    [Fact]
    public void Name_Always_EqualsPluginNameInManifests()
    {
        // Arrange
        var marketplaceManifest = ReadManifest("marketplace.json");
        var pluginManifest = ReadManifest("plugin.json");

        // Act
        var marketplacePluginNames = marketplaceManifest["plugins"]!.AsArray()
            .Select(x => x?["name"]?.GetValue<string>())
            .ToArray();
        var pluginName = pluginManifest["name"]?.GetValue<string>();

        // Assert
        marketplacePluginNames
            .Should().Equal(ShadeKitPlugin.Name);

        pluginName
            .Should().Be(ShadeKitPlugin.Name);
    }

    [Fact]
    public void Id_Always_IsPluginNameAtMarketplaceName()
    {
        // Act
        const string id = ShadeKitPlugin.Id;

        // Assert
        id
            .Should().Be("shade-kit@creativecoders-shade-kit");
    }

    private static JsonNode ReadManifest(string fileName)
    {
        var path = Path.Combine(ShippedSkillFiles.FindRepositoryRoot(), ".claude-plugin", fileName);

        return JsonNode.Parse(File.ReadAllText(path))!;
    }
}
