using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations.Install;

/// <summary>
/// Represents the options of the <see cref="IntegrationsInstallCommand" />.
/// </summary>
public class IntegrationsInstallCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the id of the agent to install the SHADE kit integration for.
    /// </summary>
    /// <value>
    /// The id of a supported agent, <c>claude</c>, <c>copilot</c>, or <c>codex</c>, as listed in
    /// <see cref="AgentOptionConverter.SupportedAgentIds" />, or <see langword="null" /> to install for every detected
    /// agent. <c>--agent</c> without a value or with an unknown id is rejected while the arguments are read (see
    /// <see cref="AgentOptionConverter" />). The default is <see langword="null" />.
    /// </value>
    [OptionParameter("agent",
        HelpText = "The agent to install for: 'claude', 'copilot', or 'codex'; without it, every detected agent",
        Converter = typeof(AgentOptionConverter))]
    public string? Agent { get; set; }
}
