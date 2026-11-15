using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Represents the SHADE kit integrations that are installed in the agents for the user.
/// </summary>
/// <param name="Integrations">
/// The installed integrations in the order in which the agents are registered; an empty list if none is installed or
/// no agent could be asked.
/// </param>
/// <param name="Warnings">
/// The warnings, for example an <see cref="Warnings.AgentUnavailableWarning" /> for each agent whose CLI is missing or
/// fails; an empty list if there were none.
/// </param>
public sealed record InstalledIntegrationsResult(
    IReadOnlyList<InstalledIntegration> Integrations,
    IReadOnlyList<KitWarning> Warnings);
