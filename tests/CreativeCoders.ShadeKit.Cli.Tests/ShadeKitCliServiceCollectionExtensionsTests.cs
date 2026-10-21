using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Testing;

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
        services.AddShadeKitCli(jsonWriter, A.Fake<ICliCancellation>());

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<ICliJsonWriter>()
            .Should().BeSameAs(jsonWriter);
    }

    [Theory]
    [InlineData(typeof(ICliJsonWriter))]
    [InlineData(typeof(CliErrorMapper))]
    [InlineData(typeof(ICliCancellation))]
    public void AddShadeKitCli_WithCoreServices_RegistersServiceAsSingleton(Type serviceType)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddShadeKitCore();

        // Act
        services.AddShadeKitCli(A.Fake<ICliJsonWriter>(), A.Fake<ICliCancellation>());

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService(serviceType)
            .Should().BeSameAs(serviceProvider.GetRequiredService(serviceType));
    }

    [Fact]
    public void AddShadeKitCli_WithConsole_RegistersConsoleConfirmationAsSingleton()
    {
        // Arrange
        using var console = new TestConsole();
        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);

        // Act
        services.AddShadeKitCli(A.Fake<ICliJsonWriter>(), A.Fake<ICliCancellation>());

        // Assert
        using var serviceProvider = services.BuildServiceProvider();
        var confirmation = serviceProvider.GetRequiredService<IConsoleConfirmation>();

        confirmation
            .Should().BeOfType<ConsoleConfirmation>()
            .And.BeSameAs(serviceProvider.GetRequiredService<IConsoleConfirmation>());
    }

    [Fact]
    public void AddShadeKitCli_ExistingConsoleConfirmationRegistration_KeepsExistingRegistration()
    {
        // Arrange
        var existingConfirmation = A.Fake<IConsoleConfirmation>();
        var services = new ServiceCollection();
        services.AddSingleton(existingConfirmation);

        // Act
        services.AddShadeKitCli(A.Fake<ICliJsonWriter>(), A.Fake<ICliCancellation>());

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<IConsoleConfirmation>()
            .Should().BeSameAs(existingConfirmation);
    }

    [Fact]
    public void AddShadeKitCli_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddShadeKitCli(A.Fake<ICliJsonWriter>(), A.Fake<ICliCancellation>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("services");
    }

    [Fact]
    public void AddShadeKitCli_NullJsonWriter_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServiceCollection().AddShadeKitCli(null!, A.Fake<ICliCancellation>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jsonWriter");
    }

    [Fact]
    public void AddShadeKitCli_ServiceCollection_RegistersGivenCancellation()
    {
        // Arrange
        var cancellation = A.Fake<ICliCancellation>();
        var services = new ServiceCollection();

        // Act
        services.AddShadeKitCli(A.Fake<ICliJsonWriter>(), cancellation);

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<ICliCancellation>()
            .Should().BeSameAs(cancellation);
    }

    [Fact]
    public void AddShadeKitCli_ExistingCancellationRegistration_KeepsExistingRegistration()
    {
        // Arrange
        var existingCancellation = A.Fake<ICliCancellation>();
        var services = new ServiceCollection();
        services.AddSingleton(existingCancellation);

        // Act
        services.AddShadeKitCli(A.Fake<ICliJsonWriter>(), A.Fake<ICliCancellation>());

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<ICliCancellation>()
            .Should().BeSameAs(existingCancellation);
    }

    [Fact]
    public void AddShadeKitCli_NullCancellation_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServiceCollection().AddShadeKitCli(A.Fake<ICliJsonWriter>(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cancellation");
    }

    [Fact]
    public void AddShadeKitCli_ExistingJsonWriterRegistration_KeepsExistingRegistration()
    {
        // Arrange
        var existingWriter = A.Fake<ICliJsonWriter>();
        var services = new ServiceCollection();
        services.AddSingleton(existingWriter);

        // Act
        services.AddShadeKitCli(A.Fake<ICliJsonWriter>(), A.Fake<ICliCancellation>());

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
        var result = services.AddShadeKitCli(A.Fake<ICliJsonWriter>(), A.Fake<ICliCancellation>());

        // Assert
        result.Should().BeSameAs(services);
    }
}
