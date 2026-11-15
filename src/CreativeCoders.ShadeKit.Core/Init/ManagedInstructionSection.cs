namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Defines the managed SHADE kit section that an <see cref="IInstructionSectionPlanner" /> plans for an agent
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
    /// <remarks>
    /// The text tells the agent that phases start only when the user calls a phase skill, the call form of a skill
    /// in each supported agent, where to see the next step, how to call the engine, and that feature state changes
    /// only through engine commands. Only the markers identify the
    /// section, so a section with an earlier text is replaced as a whole.
    /// </remarks>
    public const string Text =
        BeginMarkerPrefix + " - managed by shade-kit, content between the markers is replaced on upgrade -->\n" +
        "## SHADE kit\n" +
        "\n" +
        "This repository uses SHADE kit. Its data lives in `.shadekit/` (configuration, templates, features) and is " +
        "committed with the code.\n" +
        "\n" +
        "- A phase starts only when the user calls its phase skill (`/shade-kit-<phase>`). Never start a phase " +
        "skill on your own or from another skill.\n" +
        "- Call a skill as `/shade-kit-<skill>` in Claude Code and GitHub Copilot CLI and as " +
        "`$shade-kit:shade-kit-<skill>` in OpenAI Codex CLI; write every skill name in SHADE kit output (for " +
        "example in a closing message) in the form of the agent you run in.\n" +
        "- To see where a feature stands and which skill to call next, use the `shade-kit-next-phase` skill.\n" +
        "- Call the engine as `shade-kit <command> --json` and act on its JSON result.\n" +
        "- Change feature state only through `shade-kit` commands, never by editing `.shadekit/features/` by hand.\n" +
        EndMarker;
}
