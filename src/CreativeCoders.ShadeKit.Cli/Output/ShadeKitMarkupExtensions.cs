using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.SysConsole.Core;

namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Provides the Spectre.Console markup of the plain output that <see cref="AnsiConsoleStringExtensions" /> does not
/// cover.
/// </summary>
/// <remarks>
/// Like <see cref="AnsiConsoleStringExtensions" />, the string methods wrap markup and do not escape it, so they can
/// be nested; escape every dynamic value with <see cref="AnsiConsoleStringExtensions.ToEscapedMarkup" /> first. The
/// enum methods escape the JSON name of the value themselves.
/// </remarks>
internal static class ShadeKitMarkupExtensions
{
    /// <summary>
    /// Wraps markup in the style of secondary text, such as a missing value.
    /// </summary>
    /// <param name="markup">The markup to wrap; dynamic parts must already be escaped.</param>
    /// <returns>The markup in grey.</returns>
    public static string ToMutedMarkup(this string markup)
    {
        return $"[grey]{markup}[/]";
    }

    /// <summary>
    /// Wraps markup in the style of a command that the user can run.
    /// </summary>
    /// <param name="markup">The markup to wrap; dynamic parts must already be escaped.</param>
    /// <returns>The markup in cyan.</returns>
    public static string ToCommandMarkup(this string markup)
    {
        return $"[cyan]{markup}[/]";
    }

    /// <summary>
    /// Creates the markup of the JSON name of a phase state, colored by its meaning.
    /// </summary>
    /// <param name="state">The phase state.</param>
    /// <returns>
    /// The escaped JSON name: <c>pending</c> muted, <c>in-progress</c> as info, <c>awaiting-approval</c> as warning,
    /// <c>approved</c> as success, any other value without color.
    /// </returns>
    public static string ToMarkup(this PhaseState state)
    {
        var name = state.ToJsonName().ToEscapedMarkup();

        return state switch
        {
            PhaseState.Pending => name.ToMutedMarkup(),
            PhaseState.InProgress => name.ToInfoMarkup(),
            PhaseState.AwaitingApproval => name.ToWarningMarkup(),
            PhaseState.Approved => name.ToSuccessMarkup(),
            _ => name
        };
    }

    /// <summary>
    /// Creates the markup of the JSON name of a feature status, colored by its meaning.
    /// </summary>
    /// <param name="status">The feature status.</param>
    /// <returns>
    /// The escaped JSON name: <c>active</c> as info, <c>done</c> as success, any other value without color.
    /// </returns>
    public static string ToMarkup(this FeatureStatus status)
    {
        var name = status.ToJsonName().ToEscapedMarkup();

        return status switch
        {
            FeatureStatus.Active => name.ToInfoMarkup(),
            FeatureStatus.Done => name.ToSuccessMarkup(),
            _ => name
        };
    }

    /// <summary>
    /// Creates the markup of the JSON name of the action taken on a file by <c>init</c>, colored by its meaning.
    /// </summary>
    /// <param name="action">The action taken on the file.</param>
    /// <returns>
    /// The escaped JSON name: <c>created</c> as success, <c>appended</c> and <c>updated</c> as warning, any other
    /// value without color.
    /// </returns>
    public static string ToMarkup(this InitializedFileAction action)
    {
        var name = action.ToJsonName().ToEscapedMarkup();

        return action switch
        {
            InitializedFileAction.Created => name.ToSuccessMarkup(),
            InitializedFileAction.Appended or InitializedFileAction.Updated => name.ToWarningMarkup(),
            _ => name
        };
    }
}
