using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Store;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;

namespace CreativeCoders.ShadeKit.Core.Tests;

public class ShadeKitCoreServiceCollectionExtensionsTests
{
    [Fact]
    public void AddShadeKitCore_ServiceCollection_ResolvesKitStoreFactory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddShadeKitCore();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<IKitStoreFactory>()
            .Should().BeOfType<KitStoreFactory>();
    }

    [Fact]
    public void AddShadeKitCore_ServiceCollection_RegistersKitStoreFactoryAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddShadeKitCore();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        var firstInstance = serviceProvider.GetRequiredService<IKitStoreFactory>();
        var secondInstance = serviceProvider.GetRequiredService<IKitStoreFactory>();

        secondInstance.Should().BeSameAs(firstInstance);
    }

    [Fact]
    public void AddShadeKitCore_ServiceCollection_ReturnsSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddShadeKitCore();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddShadeKitCore_CalledTwice_RegistersKitStoreFactoryOnlyOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddShadeKitCore();
        services.AddShadeKitCore();

        // Assert
        services.Where(x => x.ServiceType == typeof(IKitStoreFactory))
            .Should().ContainSingle();
    }

    [Fact]
    public void AddShadeKitCore_ExistingKitStoreFactoryRegistration_KeepsExistingRegistration()
    {
        // Arrange
        var existingFactory = A.Fake<IKitStoreFactory>();
        var services = new ServiceCollection();
        services.AddSingleton(existingFactory);

        // Act
        services.AddShadeKitCore();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<IKitStoreFactory>()
            .Should().BeSameAs(existingFactory);
    }

    [Fact]
    public void AddShadeKitCore_NullServiceCollection_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddShadeKitCore();

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("services");
    }
}
