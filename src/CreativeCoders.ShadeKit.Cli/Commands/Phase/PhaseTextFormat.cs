using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase;

/// <summary>
/// Formats the parts of the results of the <c>shade-kit phase</c> commands for the plain output.
/// </summary>
internal static class PhaseTextFormat
{
    /// <summary>
    /// Represents the text written for a missing value.
    /// </summary>
    public const string None = "none";

    /// <summary>
    /// Formats a transition.
    /// </summary>
    /// <param name="transition">The transition to format.</param>
    /// <returns>The text <c>&lt;from&gt; -&gt; &lt;to&gt; (&lt;kind&gt;)</c>, for example <c>refine -&gt; map (forward)</c>.</returns>
    public static string FormatTransition(PhaseTransitionData transition)
    {
        return $"{transition.From} -> {transition.To} ({transition.Kind.ToJsonName()})";
    }

    /// <summary>
    /// Formats a phase with its state.
    /// </summary>
    /// <param name="phase">The phase to format.</param>
    /// <returns>The text <c>&lt;name&gt; (&lt;state&gt;)</c>, for example <c>refine (in-progress)</c>.</returns>
    public static string FormatPhase(PhaseSnapshotData phase)
    {
        return $"{phase.Name} ({phase.State.ToJsonName()})";
    }

    /// <summary>
    /// Formats the approval and the checklist of a gate.
    /// </summary>
    /// <param name="approval">
    /// <see langword="true" /> if the transition needs an approval; otherwise, <see langword="false" />.
    /// </param>
    /// <param name="checklist">The checklist of the gate, or <see langword="null" /> if it has none.</param>
    /// <returns>
    /// The text <c>approval: required|not required, checklist: &lt;checklist&gt;|none</c>.
    /// </returns>
    public static string FormatGate(bool approval, string? checklist)
    {
        return $"approval: {(approval ? "required" : "not required")}, checklist: {checklist ?? None}";
    }

    /// <summary>
    /// Formats a list of skill names.
    /// </summary>
    /// <param name="skills">The skill names in the order in which they are loaded.</param>
    /// <returns>The names separated by <c>, </c>, or <c>none</c> if <paramref name="skills" /> is empty.</returns>
    public static string FormatSkills(IReadOnlyList<string> skills)
    {
        return skills.Count == 0 ? None : string.Join(", ", skills);
    }
}
