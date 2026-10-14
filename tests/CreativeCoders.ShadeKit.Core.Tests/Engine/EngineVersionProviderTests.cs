using System.Reflection;
using System.Reflection.Emit;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Engine;

namespace CreativeCoders.ShadeKit.Core.Tests.Engine;

public class EngineVersionProviderTests
{
    [Fact]
    public void Version_CoreAssembly_ReturnsVersionWithoutBuildMetadata()
    {
        // Arrange
        var provider = new EngineVersionProvider();

        // Act
        var version = provider.Version;

        // Assert
        version
            .Should().NotBeEmpty()
            .And.NotContain("+");
    }

    [Theory]
    [InlineData("1.2.3+4355d63c184d1586407d6b3c714ff47e07b3e1f1", "1.2.3")]
    [InlineData("1.2.3-beta.1+sha.abc", "1.2.3-beta.1")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3+", "1.2.3")]
    [InlineData("1.2.3+abc+def", "1.2.3")]
    [InlineData("", "9.9.9.9")]
    [InlineData("+abc", "9.9.9.9")]
    [InlineData(" ", "9.9.9.9")]
    [InlineData(" +abc", "9.9.9.9")]
    public void Version_InformationalVersion_ReturnsVersionWithoutBuildMetadata(string informationalVersion,
        string expectedVersion)
    {
        // Arrange
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("EngineVersionProviderTests.Informational") { Version = new Version(9, 9, 9, 9) },
            AssemblyBuilderAccess.Run);

        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!,
            [informationalVersion]));

        // Act
        var version = new EngineVersionProvider(assembly).Version;

        // Assert
        version
            .Should().Be(expectedVersion);
    }

    [Fact]
    public void Version_AssemblyWithoutInformationalVersion_ReturnsAssemblyVersion()
    {
        // Arrange
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("EngineVersionProviderTests.Dynamic") { Version = new Version(1, 2, 3, 4) },
            AssemblyBuilderAccess.Run);

        // Act
        var version = new EngineVersionProvider(assembly).Version;

        // Assert
        version
            .Should().Be("1.2.3.4");
    }

    [Fact]
    public void Ctor_NullAssembly_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new EngineVersionProvider(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("assembly");
    }
}
