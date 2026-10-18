using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Formats;
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
    /// <see cref="IKitStoreFactory" />, <see cref="IGitRepositoryLocator" />, <see cref="IFormatRegistry" /> (with the
    /// file types of <see cref="FormatRegistry.CreateDefault" />), <see cref="IVersionedJsonSerializer" />,
    /// <see cref="IEngineVersionProvider" />, <see cref="IRepositoryInitializer" />,
    /// <see cref="IShippedTemplateReader" />, <see cref="IGitIgnoreWriter" />, and
    /// <see cref="IInstructionSectionWriter" /> are registered as singletons. Existing registrations are kept, so
    /// calling this method more than once has no additional effect, and a registration made before the call replaces
    /// the default.
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
        services.TryAddSingleton<IGitIgnoreWriter, GitIgnoreWriter>();
        services.TryAddSingleton<IInstructionSectionWriter, InstructionSectionWriter>();

        return services;
    }
}
