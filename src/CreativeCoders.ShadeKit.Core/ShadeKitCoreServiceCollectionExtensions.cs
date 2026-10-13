using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CreativeCoders.ShadeKit.Core;

/// <summary>
/// Provides extension methods for registering the SHADE kit core services in an <see cref="IServiceCollection" />.
/// </summary>
public static class ShadeKitCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SHADE kit core services, such as the <see cref="IKitStoreFactory" />, in the specified service collection.
    /// </summary>
    /// <remarks>
    /// <see cref="IKitStoreFactory" /> is registered as a singleton. Existing registrations are kept, so calling this
    /// method more than once has no additional effect.
    /// </remarks>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    public static IServiceCollection AddShadeKitCore(this IServiceCollection services)
    {
        Ensure.NotNull(services);

        services.TryAddSingleton<IKitStoreFactory, KitStoreFactory>();

        return services;
    }
}
