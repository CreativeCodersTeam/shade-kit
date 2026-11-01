using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.SysConsole.Core;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase;

/// <summary>
/// Formats the parts of the results of the <c>shade-kit phase</c> commands as Spectre.Console markup for the plain
/// output.
/// </summary>
/// <remarks>
/// Every method escapes the values it formats, so its result can be written with <c>MarkupLine</c> as it is.
/// </remarks>
internal static class PhaseTextFormat
{
    /// <summary>
    /// Represents the markup written for a missing value: <c>none</c>, muted.
    /// </summary>
    public static readonly string NoneMarkup = "none".ToMutedMarkup();

    /// <summary>
    /// Formats the name of a phase.
    /// </summary>
    /// <param name="name">The name of the phase.</param>
    /// <returns>The escaped name as info markup.</returns>
    public static string FormatPhaseName(string name)
    {
        return name.ToEscapedMarkup().ToInfoMarkup();
    }

    /// <summary>
    /// Formats a transition.
    /// </summary>
    /// <param name="transition">The transition to format.</param>
    /// <returns>
    /// The markup of the text <c>&lt;from&gt; -&gt; &lt;to&gt; (&lt;kind&gt;)</c>, for example
    /// <c>refine -&gt; map (forward)</c>.
    /// </returns>
    public static string FormatTransition(PhaseTransitionData transition)
    {
        return $"{FormatPhaseName(transition.From)} -> {FormatPhaseName(transition.To)} " +
               $"({transition.Kind.ToJsonName().ToEscapedMarkup()})";
    }

    /// <summary>
    /// Formats a phase with its state.
    /// </summary>
    /// <param name="phase">The phase to format.</param>
    /// <returns>
    /// The markup of the text <c>&lt;name&gt; (&lt;state&gt;)</c>, for example <c>refine (in-progress)</c>.
    /// </returns>
    public static string FormatPhase(PhaseSnapshotData phase)
    {
        return $"{FormatPhaseName(phase.Name)} ({phase.State.ToMarkup()})";
    }

    /// <summary>
    /// Formats the approval and the checklist of a gate.
    /// </summary>
    /// <param name="approval">
    /// <see langword="true" /> if the transition needs an approval; otherwise, <see langword="false" />.
    /// </param>
    /// <param name="checklist">The checklist of the gate, or <see langword="null" /> if it has none.</param>
    /// <returns>
    /// The markup of the text <c>approval: required|not required, checklist: &lt;checklist&gt;|none</c>; a required
    /// approval is highlighted as warning.
    /// </returns>
    public static string FormatGate(bool approval, string? checklist)
    {
        var approvalMarkup = approval ? "required".ToWarningMarkup() : "not required";
        var checklistMarkup = checklist is null ? NoneMarkup : checklist.ToEscapedMarkup();

        return $"approval: {approvalMarkup}, checklist: {checklistMarkup}";
    }

    /// <summary>
    /// Formats a list of skill names.
    /// </summary>
    /// <param name="skills">The skill names in the order in which they are loaded.</param>
    /// <returns>
    /// The escaped names separated by <c>, </c>, or <see cref="NoneMarkup" /> if <paramref name="skills" /> is empty.
    /// </returns>
    public static string FormatSkills(IReadOnlyList<string> skills)
    {
        return skills.Count == 0 ? NoneMarkup : string.Join(", ", skills.Select(x => x.ToEscapedMarkup()));
    }
}
