using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;

namespace CreativeCoders.ShadeKit.Cli.Tests;

public class ShadeKitCliServiceCollectionExtensionsTests
{
    [Fact]
    public void AddShadeKitCli_ServiceCollection_RegistersGivenJsonWriter()
    {
        // Arrange
        var jsonWriter = A.Fake<ICliJsonWriter>();
        var services = new ServiceCollection();

        // Act
        services.AddShadeKitCli(jsonWriter);

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<ICliJsonWriter>()
            .Should().BeSameAs(jsonWriter);
    }

    [Theory]
    [InlineData(typeof(ICliJsonWriter))]
    [InlineData(typeof(CliErrorMapper))]
    public void AddShadeKitCli_WithCoreServices_RegistersServiceAsSingleton(Type serviceType)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddShadeKitCore();

        // Act
        services.AddShadeKitCli(A.Fake<ICliJsonWriter>());

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService(serviceType)
            .Should().BeSameAs(serviceProvider.GetRequiredService(serviceType));
    }

    [Fact]
    public void AddShadeKitCli_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddShadeKitCli(A.Fake<ICliJsonWriter>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("services");
    }

    [Fact]
    public void AddShadeKitCli_NullJsonWriter_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServiceCollection().AddShadeKitCli(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jsonWriter");
    }

    [Fact]
    public void AddShadeKitCli_ExistingJsonWriterRegistration_KeepsExistingRegistration()
    {
        // Arrange
        var existingWriter = A.Fake<ICliJsonWriter>();
        var services = new ServiceCollection();
        services.AddSingleton(existingWriter);

        // Act
        services.AddShadeKitCli(A.Fake<ICliJsonWriter>());

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<ICliJsonWriter>()
            .Should().BeSameAs(existingWriter);
    }

    [Fact]
    public void AddShadeKitCli_ServiceCollection_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddShadeKitCli(A.Fake<ICliJsonWriter>());

        // Assert
        result.Should().BeSameAs(services);
    }
}
