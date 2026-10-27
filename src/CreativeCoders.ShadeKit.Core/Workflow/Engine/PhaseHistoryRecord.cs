using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents the content of the <c>history</c> entry that a state change of the phases adds, without the time and
/// the git user, which the caller adds with <see cref="ToEntry" />.
/// </summary>
/// <param name="Action">One of the enumeration values that specifies the kind of the state change.</param>
/// <param name="From">The phase that was current before the state change, or <see langword="null" /> if none.</param>
/// <param name="To">
/// The target phase of the transition, or <see langword="null" /> if the state change has no target, as with
/// <see cref="PhaseHistoryAction.Complete" />.
/// </param>
/// <param name="Note">The note given with the state change, or <see langword="null" /> if none.</param>
public sealed record PhaseHistoryRecord(PhaseHistoryAction Action, string? From, string? To, string? Note)
{
    /// <summary>
    /// Creates the <c>history</c> entry of the state change.
    /// </summary>
    /// <param name="at">The point in time of the state change.</param>
    /// <param name="by">The git user who caused the state change.</param>
    /// <returns>A new entry with <paramref name="at" />, <paramref name="by" />, and the values of this record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="by" /> is <see langword="null" />.</exception>
    public PhaseHistoryEntry ToEntry(DateTimeOffset at, PhaseHistoryActor by)
    {
        return new PhaseHistoryEntry
        {
            At = at,
            By = Ensure.NotNull(by),
            Action = Action,
            From = From,
            To = To,
            Note = Note
        };
    }
}
