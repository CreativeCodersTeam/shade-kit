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
    /// Registers the services that commands use to report results and errors, to ask the user, and to notice that the
    /// user canceled the run.
    /// </summary>
    /// <remarks>
    /// All services are singletons; existing registrations are kept. The caller registers an
    /// <see cref="Spectre.Console.IAnsiConsole" /> and the services of
    /// <see cref="CreativeCoders.ShadeKit.Core.ShadeKitCoreServiceCollectionExtensions.AddShadeKitCore" />.
    /// </remarks>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="jsonWriter">The writer that commands use for their JSON output.</param>
    /// <param name="cancellation">The cancellation of the run, whose token commands pass to the core.</param>
    /// <returns>The same <see cref="IServiceCollection" /> instance so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services" />, <paramref name="jsonWriter" />, or <paramref name="cancellation" /> is
    /// <see langword="null" />.
    /// </exception>
    public static IServiceCollection AddShadeKitCli(this IServiceCollection services, ICliJsonWriter jsonWriter,
        ICliCancellation cancellation)
    {
        Ensure.NotNull(services);
        Ensure.NotNull(jsonWriter);
        Ensure.NotNull(cancellation);

        services.TryAddSingleton(jsonWriter);
        services.TryAddSingleton(cancellation);
        services.TryAddSingleton<CliErrorMapper>();
        services.TryAddSingleton<IConsoleConfirmation, ConsoleConfirmation>();
        services.TryAddSingleton<ShadeKitCommandContext>();

        return services;
    }
}
