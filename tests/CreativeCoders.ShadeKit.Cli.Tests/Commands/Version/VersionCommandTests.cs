using System.Reflection;
using System.Reflection.Emit;
using AwesomeAssertions;
using CreativeCoders.Cli.Core;
using CreativeCoders.ShadeKit.Cli.Commands.Version;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Version;

public class VersionCommandTests
{
    [Fact]
    public async Task ExecuteAsync_Called_WritesToolNameAndVersionWithoutBuildMetadata()
    {
        // Arrange
        var expectedVersion = VersionCommand.GetVersion(typeof(VersionCommand).Assembly);

        var console = new TestConsole();
        var command = new VersionCommand(console);

        // Act
        await command.ExecuteAsync();

        // Assert
        console.Output.TrimEnd()
            .Should().Be($"shade-kit {expectedVersion}");

        expectedVersion
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
    public void GetVersion_InformationalVersion_ReturnsVersionWithoutBuildMetadata(string informationalVersion,
        string expectedVersion)
    {
        // Arrange
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("VersionCommandTests.Informational") { Version = new System.Version(9, 9, 9, 9) },
            AssemblyBuilderAccess.Run);

        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!,
            [informationalVersion]));

        // Act
        var version = VersionCommand.GetVersion(assembly);

        // Assert
        version
            .Should().Be(expectedVersion);
    }

    [Fact]
    public async Task ExecuteAsync_Called_ReturnsSuccess()
    {
        // Arrange
        var command = new VersionCommand(new TestConsole());

        // Act
        var result = await command.ExecuteAsync();

        // Assert
        result.ExitCode
            .Should().Be(0);
    }

    [Fact]
    public void GetVersion_AssemblyWithoutInformationalVersion_ReturnsAssemblyVersion()
    {
        // Arrange
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("VersionCommandTests.Dynamic") { Version = new System.Version(1, 2, 3, 4) },
            AssemblyBuilderAccess.Run);

        // Act
        var version = VersionCommand.GetVersion(assembly);

        // Assert
        version
            .Should().Be("1.2.3.4");
    }

    [Fact]
    public void Ctor_NullConsole_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new VersionCommand(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ansiConsole");
    }

    [Fact]
    public void VersionCommand_CliCommandAttribute_IsRegisteredAsTopLevelVersionCommand()
    {
        // Act
        var attribute = typeof(VersionCommand).GetCustomAttribute<CliCommandAttribute>();

        // Assert
        attribute
            .Should().NotBeNull();

        attribute!.Commands
            .Should().Equal("version");
    }
}
