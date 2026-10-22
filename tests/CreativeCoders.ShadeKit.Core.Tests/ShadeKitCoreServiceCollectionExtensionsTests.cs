using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Templates;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

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

    [Theory]
    [InlineData(typeof(IGitRepositoryLocator), typeof(GitRepositoryLocator))]
    [InlineData(typeof(IFormatRegistry), typeof(FormatRegistry))]
    [InlineData(typeof(IVersionedJsonSerializer), typeof(VersionedJsonSerializer))]
    [InlineData(typeof(IEngineVersionProvider), typeof(EngineVersionProvider))]
    [InlineData(typeof(IRepositoryInitializer), typeof(RepositoryInitializer))]
    [InlineData(typeof(IShippedTemplateReader), typeof(ShippedTemplateReader))]
    [InlineData(typeof(IGitIgnorePlanner), typeof(GitIgnorePlanner))]
    [InlineData(typeof(IInstructionSectionPlanner), typeof(InstructionSectionPlanner))]
    [InlineData(typeof(IGitExecutableLocator), typeof(GitExecutableLocator))]
    [InlineData(typeof(IGitCommandRunner), typeof(GitCommandRunner))]
    [InlineData(typeof(IGitRepositoryInfo), typeof(GitRepositoryInfo))]
    [InlineData(typeof(IFeatureIdFactory), typeof(FeatureIdFactory))]
    [InlineData(typeof(ICurrentFeatureResolver), typeof(CurrentFeatureResolver))]
    [InlineData(typeof(IFeatureStore), typeof(FeatureStore))]
    public void AddShadeKitCore_ServiceCollection_RegistersServiceAsSingleton(Type serviceType, Type implementationType)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddShadeKitCore();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        var firstInstance = serviceProvider.GetRequiredService(serviceType);
        var secondInstance = serviceProvider.GetRequiredService(serviceType);

        firstInstance.Should().BeOfType(implementationType);
        secondInstance.Should().BeSameAs(firstInstance);
    }

    [Fact]
    public void AddShadeKitCore_ServiceCollection_RegistersFormatRegistryWithAllFileTypes()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddShadeKitCore();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<IFormatRegistry>().Formats.Select(x => x.Id)
            .Should().Equal(FormatIds.Config, FormatIds.Gates, FormatIds.Feature, FormatIds.CurrentFeature,
                FormatIds.TemplatesLock, FormatIds.Integrations);
    }

    [Fact]
    public void AddShadeKitCore_ExistingFormatRegistryRegistration_KeepsExistingRegistration()
    {
        // Arrange
        var existingRegistry = A.Fake<IFormatRegistry>();
        var services = new ServiceCollection();
        services.AddSingleton(existingRegistry);

        // Act
        services.AddShadeKitCore();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<IFormatRegistry>()
            .Should().BeSameAs(existingRegistry);
    }

    [Fact]
    public void AddShadeKitCore_ExistingFormatRegistryRegistration_SerializerUsesExistingRegistry()
    {
        // Arrange
        var existingRegistry = A.Fake<IFormatRegistry>();
        A.CallTo(() => existingRegistry.GetFormat(A<string>._))
            .Returns(new FormatDefinition("custom", new Dictionary<int, string> { [1] = "7.0.0" }));

        var services = new ServiceCollection();
        services.AddSingleton(existingRegistry);

        // Act
        services.AddShadeKitCore();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        var json = serviceProvider.GetRequiredService<IVersionedJsonSerializer>()
            .Serialize(new TestDocument(), "custom");

        json.Should().Contain("\"minEngineVersion\": \"7.0.0\"");
    }

    [Theory]
    [InlineData(typeof(IGitRepositoryLocator))]
    [InlineData(typeof(IVersionedJsonSerializer))]
    [InlineData(typeof(IEngineVersionProvider))]
    [InlineData(typeof(IRepositoryInitializer))]
    [InlineData(typeof(IShippedTemplateReader))]
    [InlineData(typeof(IGitIgnorePlanner))]
    [InlineData(typeof(IInstructionSectionPlanner))]
    [InlineData(typeof(IGitExecutableLocator))]
    [InlineData(typeof(IGitCommandRunner))]
    [InlineData(typeof(IGitRepositoryInfo))]
    [InlineData(typeof(IFeatureIdFactory))]
    [InlineData(typeof(ICurrentFeatureResolver))]
    [InlineData(typeof(IFeatureStore))]
    [InlineData(typeof(TimeProvider))]
    public void AddShadeKitCore_ExistingServiceRegistration_KeepsExistingRegistration(Type serviceType)
    {
        // Arrange
        var existingService = FakeItEasy.Sdk.Create.Fake(serviceType);
        var services = new ServiceCollection();
        services.AddSingleton(serviceType, existingService);

        // Act
        services.AddShadeKitCore();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService(serviceType)
            .Should().BeSameAs(existingService);
    }

    [Theory]
    [InlineData(typeof(IGitRepositoryLocator))]
    [InlineData(typeof(IFormatRegistry))]
    [InlineData(typeof(IVersionedJsonSerializer))]
    [InlineData(typeof(IEngineVersionProvider))]
    [InlineData(typeof(IRepositoryInitializer))]
    [InlineData(typeof(IShippedTemplateReader))]
    [InlineData(typeof(IGitIgnorePlanner))]
    [InlineData(typeof(IInstructionSectionPlanner))]
    [InlineData(typeof(IGitExecutableLocator))]
    [InlineData(typeof(IGitCommandRunner))]
    [InlineData(typeof(IGitRepositoryInfo))]
    [InlineData(typeof(IFeatureIdFactory))]
    [InlineData(typeof(ICurrentFeatureResolver))]
    [InlineData(typeof(IFeatureStore))]
    [InlineData(typeof(TimeProvider))]
    public void AddShadeKitCore_CalledTwice_RegistersServiceOnlyOnce(Type serviceType)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddShadeKitCore();
        services.AddShadeKitCore();

        // Assert
        services.Where(x => x.ServiceType == serviceType)
            .Should().ContainSingle();
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
    public void AddShadeKitCore_ExistingGitRepositoryLocatorRegistration_KitStoreFactoryUsesExistingLocator()
    {
        // Arrange
        var existingLocator = A.Fake<IGitRepositoryLocator>();
        A.CallTo(() => existingLocator.GetRepositoryRootPath("/start"))
            .Throws(new NotAGitRepositoryException("/start"));

        var services = new ServiceCollection();
        services.AddSingleton(existingLocator);

        // Act
        services.AddShadeKitCore();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        var act = () => serviceProvider.GetRequiredService<IKitStoreFactory>().Create("/start");

        act.Should().Throw<NotAGitRepositoryException>();

        A.CallTo(() => existingLocator.GetRepositoryRootPath("/start"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AddShadeKitCore_ExistingGitCommandRunnerRegistration_GitRepositoryInfoUsesExistingRunner()
    {
        // Arrange
        var existingRunner = A.Fake<IGitCommandRunner>();
        A.CallTo(() => existingRunner.RunAsync("/repo", A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .Returns(new GitCommandResult(0, "main\n", ""));

        var services = new ServiceCollection();
        services.AddSingleton(existingRunner);

        // Act
        services.AddShadeKitCore();

        // Assert
        await using var serviceProvider = services.BuildServiceProvider();

        var branch = await serviceProvider.GetRequiredService<IGitRepositoryInfo>()
            .GetCurrentBranchAsync("/repo", TestContext.Current.CancellationToken);

        branch
            .Should().Be("main");
    }

    [Fact]
    public async Task AddShadeKitCore_ExistingGitExecutableLocatorRegistration_GitCommandRunnerUsesExistingLocator()
    {
        // Arrange
        var existingLocator = A.Fake<IGitExecutableLocator>();
        A.CallTo(() => existingLocator.FindGitExecutable()).Returns(null);

        var services = new ServiceCollection();
        services.AddSingleton(existingLocator);

        // Act
        services.AddShadeKitCore();

        // Assert
        await using var serviceProvider = services.BuildServiceProvider();

        var act = () => serviceProvider.GetRequiredService<IGitCommandRunner>()
            .RunAsync("/repo", ["config", "user.name"], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<GitUnavailableException>();

        A.CallTo(() => existingLocator.FindGitExecutable())
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AddShadeKitCore_ExistingGitRepositoryInfoRegistration_CurrentFeatureResolverUsesExistingInfo()
    {
        // Arrange
        var existingInfo = A.Fake<IGitRepositoryInfo>();
        A.CallTo(() => existingInfo.GetCurrentBranchAsync(A<string>._, A<CancellationToken>._))
            .Returns("feature/login");

        var store = A.Fake<IKitStore>();
        A.CallTo(() => store.RootPath).Returns("/repo/.shadekit");
        A.CallTo(() => store.GetFeatureNames()).Returns(["login"]);

        var services = new ServiceCollection();
        services.AddSingleton(existingInfo);

        // Act
        services.AddShadeKitCore();

        // Assert
        await using var serviceProvider = services.BuildServiceProvider();

        var featureId = await serviceProvider.GetRequiredService<ICurrentFeatureResolver>()
            .ResolveBranchFeatureIdAsync(store, TestContext.Current.CancellationToken);

        featureId
            .Should().Be("login");
    }

    [Fact]
    public void AddShadeKitCore_ServiceCollection_RegistersSystemTimeProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddShadeKitCore();

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<TimeProvider>()
            .Should().BeSameAs(TimeProvider.System);
    }

    [Fact]
    public async Task AddShadeKitCore_ExistingTimeProviderRegistration_FeatureIdFactoryUsesExistingTimeProvider()
    {
        // Arrange
        var existingTimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 10, 20, 15, 30, 0, TimeSpan.Zero));
        existingTimeProvider.SetLocalTimeZone(TimeZoneInfo.Utc);

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(existingTimeProvider);

        // Act
        services.AddShadeKitCore();

        // Assert
        await using var serviceProvider = services.BuildServiceProvider();

        var identity = await serviceProvider.GetRequiredService<IFeatureIdFactory>()
            .CreateAsync("Login SSO", null, "/repo", new Dictionary<string, string>(), "/repo",
                TestContext.Current.CancellationToken);

        identity.Id
            .Should().Be("20261020-1530-login-sso");
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

    [SuppressMessage("csharpsquid", "S2094", Justification = "Test class for serialization")]
    public sealed class TestDocument : VersionedDocument;
}
