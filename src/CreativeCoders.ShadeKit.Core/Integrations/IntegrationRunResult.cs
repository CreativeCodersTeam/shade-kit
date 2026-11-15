using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Represents the result of a successful <c>shade-kit integrations install</c> or <c>update</c> over all agents.
/// </summary>
/// <param name="Source">
/// The marketplace source that the engine targets, or <see langword="null" /> if an update found no installed
/// integration and therefore resolved no source.
/// </param>
/// <param name="Integrations">
/// The result of each agent that was installed or updated, in the order in which the agents are registered; an empty
/// list if an update found no installed integration.
/// </param>
/// <param name="Warnings">
/// The warnings of the run, for example an <see cref="Warnings.IntegrationPrereleaseRefWarning" />; an empty list if
/// there were none.
/// </param>
public sealed record IntegrationRunResult(
    MarketplaceSource? Source,
    IReadOnlyList<IntegrationResult> Integrations,
    IReadOnlyList<KitWarning> Warnings);
