using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Integrations.Claude;
using CreativeCoders.ShadeKit.Core.Integrations.Codex;
using CreativeCoders.ShadeKit.Core.Integrations.Copilot;
using CreativeCoders.SysConsole.Core;

namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations;

/// <summary>
/// Provides the <see cref="AgentPresentation" /> of every agent that the CLI supports, which the plain output, the
/// error messages, and the option <c>--agent</c> read.
/// </summary>
/// <remarks>
/// The table is the only place in the CLI that names the agents one by one; the error mapping of the exceptions of each
/// agent stays in <see cref="CliErrorMapper" />. A test checks it against the registered
/// <c>IIntegrationService.AgentIds</c>.
/// </remarks>
internal static class AgentPresentations
{
    /// <summary>
    /// Represents the sentence that the error of an incomplete Codex integration ends with.
    /// </summary>
    /// <remarks>
    /// The integration of OpenAI Codex CLI switches the marketplace whenever its check
    /// <c>codex plugin marketplace add</c> fails, because codex reports no ref; the check can also fail because codex
    /// cannot reach the marketplace.
    /// </remarks>
    public const string CodexIncompleteNote =
        "Codex counts a failed check of the marketplace as another source, so the switch may also have followed a " +
        "network, proxy, or login failure; restore the connection first.";

    /// <summary>
    /// Gets the presentations of the supported agents.
    /// </summary>
    /// <value>
    /// One presentation per agent, in the order of <c>IIntegrationService.AgentIds</c>: Claude Code,
    /// GitHub Copilot CLI, and OpenAI Codex CLI.
    /// </value>
    public static IReadOnlyList<AgentPresentation> All { get; } =
    [
        new AgentPresentation(ClaudeCodeIntegration.ClaudeAgentId, "Claude Code",
            IntegrationsTextFormat.RestartClaudeText,
            pluginId => FormatEnableCommandNote("Claude Code", "claude", pluginId)),
        new AgentPresentation(CopilotCliIntegration.CopilotAgentId, "GitHub Copilot CLI",
            IntegrationsTextFormat.RestartCopilotText,
            pluginId => FormatEnableCommandNote("GitHub Copilot CLI", "copilot", pluginId)),
        new AgentPresentation(CodexCliIntegration.CodexAgentId, "OpenAI Codex CLI",
            IntegrationsTextFormat.RestartCodexText,
            _ => $"{"The plugin is disabled in Codex.".ToWarningMarkup()} Enable it in Codex with " +
                 $"{"/plugins".ToCommandMarkup()}.",
            CodexIncompleteNote)
    ];

    /// <summary>
    /// Finds the presentation of the specified agent.
    /// </summary>
    /// <param name="agentId">The id of the agent, compared case-sensitively.</param>
    /// <returns>
    /// The presentation whose <see cref="AgentPresentation.AgentId" /> equals <paramref name="agentId" />, or
    /// <see langword="null" /> if there is none or <paramref name="agentId" /> is <see langword="null" />.
    /// </returns>
    public static AgentPresentation? Find(string? agentId)
    {
        return All.FirstOrDefault(x => string.Equals(x.AgentId, agentId, StringComparison.Ordinal));
    }

    // The agents whose CLI can enable a plugin: the note names the command with the plugin id.
    private static string FormatEnableCommandNote(string agentName, string executableName, string pluginId)
    {
        var command = $"{executableName} plugin enable {pluginId}".ToEscapedMarkup().ToCommandMarkup();

        return $"{$"The plugin is disabled in {agentName}.".ToEscapedMarkup().ToWarningMarkup()} Run '{command}' to " +
               "enable it.";
    }
}
