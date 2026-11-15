using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations;

namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit integrations install --json</c> and
/// <c>shade-kit integrations update --json</c> return.
/// </summary>
/// <param name="Ref">
/// The branch or tag of the marketplace source that the engine targets, for example <c>v0.8.0</c> or <c>main</c>, or
/// <see langword="null" /> if the source is a local directory or a GitHub repository without a branch or tag, or if
/// an update found no installed integration and resolved no source.
/// </param>
/// <param name="Source">
/// The marketplace source as the agent CLI takes it: <c>owner/repo#ref</c> or <c>owner/repo</c> for a GitHub
/// repository, for example <c>CreativeCodersTeam/shade-kit#v0.8.0</c>, or the absolute path of a local directory
/// that the environment variable <c>SHADE_KIT_MARKETPLACE_SOURCE</c> names; <see langword="null" /> if an update
/// found no installed integration and resolved no source.
/// </param>
/// <param name="Integrations">
/// The result of each agent, in the order in which the agents are registered; an empty list if an update found no
/// installed integration.
/// </param>
public sealed record IntegrationRunData(
    string? Ref,
    string? Source,
    IReadOnlyList<IntegrationResultData> Integrations)
{
    /// <summary>
    /// Creates the data of the specified run.
    /// </summary>
    /// <param name="result">The result of the install or update.</param>
    /// <returns>The data with the targeted source and the result of each agent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static IntegrationRunData From(IntegrationRunResult result)
    {
        Ensure.NotNull(result);

        return new IntegrationRunData(result.Source?.Ref, result.Source?.Text,
            [.. result.Integrations.Select(IntegrationResultData.From)]);
    }
}
