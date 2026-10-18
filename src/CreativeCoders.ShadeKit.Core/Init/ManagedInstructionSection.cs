namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Defines the managed SHADE kit section that an <see cref="IInstructionSectionWriter" /> writes into an agent
/// instruction file such as <c>CLAUDE.md</c> or <c>AGENTS.md</c>.
/// </summary>
public static class ManagedInstructionSection
{
    /// <summary>
    /// The text with which the trimmed begin marker line of the managed section starts.
    /// </summary>
    public const string BeginMarkerPrefix = "<!-- shade-kit:begin";

    /// <summary>
    /// The trimmed text of the end marker line of the managed section.
    /// </summary>
    public const string EndMarker = "<!-- shade-kit:end -->";

    /// <summary>
    /// The managed section, from the begin marker line to the end marker line, with <c>\n</c> line terminators and
    /// without a final line terminator.
    /// </summary>
    public const string Text =
        BeginMarkerPrefix + " - managed by shade-kit, content between the markers is replaced on upgrade -->\n" +
        "## SHADE kit\n" +
        "\n" +
        "This repository uses SHADE kit. Its data lives in `.shadekit/` (configuration, templates, features) and is " +
        "committed with the code.\n" +
        "\n" +
        "- To start or continue work on a feature, use the `shade-kit-next-phase` skill.\n" +
        "- Call the engine as `shade-kit <command> --json` and act on its JSON result.\n" +
        "- Change feature state only through `shade-kit` commands, never by editing `.shadekit/features/` by hand.\n" +
        EndMarker;
}
