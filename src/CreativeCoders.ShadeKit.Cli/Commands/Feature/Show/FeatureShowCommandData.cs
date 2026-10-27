using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Show;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit feature show &lt;id&gt; --json</c> returns.
/// </summary>
/// <param name="Id">The id of the feature, which is the name of its folder.</param>
/// <param name="Title">The title of the feature.</param>
/// <param name="Profile">The workflow profile of the feature.</param>
/// <param name="Status">The status of the feature, written as <c>active</c> or <c>done</c>.</param>
/// <param name="Source">The source of the request of the feature.</param>
/// <param name="CreatedAt">The point in time, with its UTC offset, at which the feature was created.</param>
/// <param name="CreatedBy">The git user who created the feature.</param>
/// <param name="Paths">The repository-relative paths of the feature folder and its files.</param>
/// <param name="CurrentPhase">
/// The current phase of the feature, normalized against its workflow profile; the first phase of the profile for a
/// feature without phase fields.
/// </param>
/// <param name="PendingTransition">
/// The target phase of the transition that waits for an approval, or <see langword="null" /> if none is pending.
/// </param>
/// <param name="Phases">
/// The state of each phase, written as a JSON object from phase name to state: the phases of the workflow profile in
/// profile order, followed by the preserved entries of phases the profile no longer has.
/// </param>
/// <param name="History">
/// The entries of the phase history as they are stored, oldest first; empty if the feature has none.
/// </param>
public sealed record FeatureShowCommandData(
    string Id,
    string Title,
    string Profile,
    FeatureStatus Status,
    FeatureSourceData Source,
    DateTimeOffset CreatedAt,
    FeatureCreatorData CreatedBy,
    FeaturePaths Paths,
    string CurrentPhase,
    string? PendingTransition,
    IReadOnlyDictionary<string, PhaseState> Phases,
    IReadOnlyList<FeatureShowCommandHistoryEntryData> History)
{
    /// <summary>
    /// Creates the data from the result of reading a feature.
    /// </summary>
    /// <param name="result">The result of reading the feature.</param>
    /// <returns>
    /// The data with the fields of the feature, the paths, and the normalized phase state of <paramref name="result" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static FeatureShowCommandData FromResult(FeatureShowResult result)
    {
        Ensure.NotNull(result);

        var feature = result.Feature;
        var phaseState = result.PhaseState;

        return new FeatureShowCommandData(result.FeatureId, feature.Title, feature.Profile, feature.Status,
            FeatureSourceData.FromSource(feature.Source), feature.CreatedAt,
            FeatureCreatorData.FromCreator(feature.CreatedBy), result.Paths, phaseState.CurrentPhase,
            phaseState.PendingTransition, new OrderedDictionary<string, PhaseState>(phaseState.Phases),
            [.. (feature.History ?? []).Select(FeatureShowCommandHistoryEntryData.FromEntry)]);
    }
}
