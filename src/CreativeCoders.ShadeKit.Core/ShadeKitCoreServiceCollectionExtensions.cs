using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CreativeCoders.ShadeKit.Core;

/// <summary>
/// Provides extension methods for registering the SHADE kit core services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ShadeKitCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SHADE kit core services, such as the <see cref="IKitStoreFactory" /> and the
    /// <see cref="IRepositoryInitializer" />, in the specified service collection.
    /// </summary>
    /// <remarks>
    /// All services are singletons. Existing registrations are kept, so a registration made before the call replaces
    /// the default and repeated calls have no effect.
    /// </remarks>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    public static IServiceCollection AddShadeKitCore(this IServiceCollection services)
    {
        Ensure.NotNull(services);

        services.TryAddSingleton<IKitStoreFactory, KitStoreFactory>();
        services.TryAddSingleton<IGitRepositoryLocator, GitRepositoryLocator>();
        services.TryAddSingleton<IFormatRegistry>(_ => FormatRegistry.CreateDefault());
        services.TryAddSingleton<IVersionedJsonSerializer, VersionedJsonSerializer>();
        services.TryAddSingleton<IEngineVersionProvider, EngineVersionProvider>();
        services.TryAddSingleton<IRepositoryInitializer, RepositoryInitializer>();
        services.TryAddSingleton<IShippedTemplateReader, ShippedTemplateReader>();
        services.TryAddSingleton<IGitIgnorePlanner, GitIgnorePlanner>();
        services.TryAddSingleton<IInstructionSectionPlanner, InstructionSectionPlanner>();
        services.TryAddSingleton<IGitExecutableLocator, GitExecutableLocator>();
        services.TryAddSingleton<IGitCommandRunner, GitCommandRunner>();
        services.TryAddSingleton<IGitRepositoryInfo, GitRepositoryInfo>();
        services.TryAddSingleton<IGitLineEndingResolver, GitLineEndingResolver>();
        services.TryAddSingleton<IEditorConfigLineEndingResolver, EditorConfigLineEndingResolver>();
        services.TryAddSingleton<ILineEndingResolver, LineEndingResolver>();
        services.TryAddSingleton<ITextFileWriter, TextFileWriter>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IFeatureIdFactory, FeatureIdFactory>();
        services.TryAddSingleton<ICurrentFeatureResolver, CurrentFeatureResolver>();
        services.TryAddSingleton<IFeatureStore, FeatureStore>();

        return services;
    }
}
