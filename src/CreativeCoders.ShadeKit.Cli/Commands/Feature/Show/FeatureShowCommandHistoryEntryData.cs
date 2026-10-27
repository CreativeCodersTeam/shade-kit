using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Show;

/// <summary>
/// Represents one entry of the <c>history</c> array in the <c>data</c> of
/// <c>shade-kit feature show &lt;id&gt; --json</c>.
/// </summary>
/// <param name="At">The point in time, with its UTC offset, at which the phase state changed.</param>
/// <param name="By">The git user who changed the phase state.</param>
/// <param name="Action">
/// The kind of the change, written as <c>start</c>, <c>approve</c>, <c>reject</c>, <c>reopen</c>, or
/// <c>complete</c>.
/// </param>
/// <param name="From">The phase the change started from, or <see langword="null" /> if the entry has none.</param>
/// <param name="To">
/// The target phase of the change, or <see langword="null" /> if the entry has none, for example for
/// <c>complete</c>.
/// </param>
/// <param name="Note">The note of the change, or <see langword="null" /> if the entry has none.</param>
public sealed record FeatureShowCommandHistoryEntryData(
    DateTimeOffset At,
    FeatureShowCommandHistoryActorData By,
    PhaseHistoryAction Action,
    string? From,
    string? To,
    string? Note)
{
    /// <summary>
    /// Creates the data from an entry of the phase history of a feature.
    /// </summary>
    /// <param name="entry">The <c>history</c> entry, as it is stored in <c>feature.json</c>.</param>
    /// <returns>The data with the fields of <paramref name="entry" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry" /> is <see langword="null" />.</exception>
    public static FeatureShowCommandHistoryEntryData FromEntry(PhaseHistoryEntry entry)
    {
        Ensure.NotNull(entry);

        return new FeatureShowCommandHistoryEntryData(entry.At,
            new FeatureShowCommandHistoryActorData(entry.By.Name, entry.By.Email), entry.Action, entry.From, entry.To,
            entry.Note);
    }
}
