namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations;

/// <summary>
/// Represents the texts with which the CLI presents the integration of one agent in its plain output and its error
/// messages.
/// </summary>
/// <remarks>
/// <see cref="AgentPresentations.All" /> holds one instance per agent of <c>IIntegrationService.AgentIds</c>, in the
/// same order.
/// </remarks>
/// <param name="AgentId">The id of the agent, for example <c>claude</c>.</param>
/// <param name="DisplayName">The name of the agent for humans, for example <c>Claude Code</c>.</param>
/// <param name="RestartText">
/// The note written after the integration of the agent was installed or updated, which says how the agent loads the
/// skills.
/// </param>
/// <param name="FormatDisabledNote">
/// The function that takes the id of the integration in the agent and returns the markup of the note that the
/// integration is disabled and how to enable it.
/// </param>
/// <param name="IncompleteNote">
/// The plain-text sentence that the error of an incomplete integration of the agent ends with, or
/// <see langword="null" /> if it adds none.
/// </param>
internal sealed record AgentPresentation(
    string AgentId,
    string DisplayName,
    string RestartText,
    Func<string, string> FormatDisabledNote,
    string? IncompleteNote = null);
