using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CreativeCoders.ShadeKit.Cli;

/// <summary>
/// Provides extension methods for registering the services of the SHADE kit CLI in an <see cref="IServiceCollection" />.
/// </summary>
internal static class ShadeKitCliServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services that commands use to report results and errors.
    /// </summary>
    /// <remarks>
    /// <paramref name="jsonWriter" /> is registered as the <see cref="ICliJsonWriter" /> singleton, and
    /// <see cref="CliErrorMapper" /> as a singleton. Existing registrations are kept.
    /// </remarks>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="jsonWriter">The writer that commands use for their JSON output.</param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services" /> or <paramref name="jsonWriter" /> is <see langword="null" />.
    /// </exception>
    public static IServiceCollection AddShadeKitCli(this IServiceCollection services, ICliJsonWriter jsonWriter)
    {
        Ensure.NotNull(services);
        Ensure.NotNull(jsonWriter);

        services.TryAddSingleton(jsonWriter);
        services.TryAddSingleton<CliErrorMapper>();

        return services;
    }
}
