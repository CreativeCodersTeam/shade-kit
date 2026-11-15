using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Claude;
using CreativeCoders.ShadeKit.Core.Integrations.Codex;
using CreativeCoders.ShadeKit.Core.Integrations.Copilot;
using CreativeCoders.SysConsole.Cli.Parsing;
using CreativeCoders.SysConsole.Cli.Parsing.OptionProperties;

namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations.Install;

/// <summary>
/// Converts the value of <c>--agent</c> to the id of a supported agent and rejects the option when it is given without
/// a value or with an unknown id.
/// </summary>
/// <remarks>
/// <para>
/// The option parser of CreativeCoders.SysConsole.Cli.Parsing creates the converter with its parameterless
/// constructor and calls it only for an option that is on the command line. It passes <see langword="null" /> as the
/// value when the option is the last argument or the next argument starts with <c>-</c>.
/// </para>
/// <para>
/// Because the parser does not resolve converters from the service provider, the converter cannot ask
/// <see cref="IIntegrationService.AgentIds" />; <see cref="SupportedAgentIds" /> lists the same ids, which a test
/// checks against the registered <see cref="IIntegrationService" />.
/// </para>
/// </remarks>
public sealed class AgentOptionConverter : ICliValueConverter
{
    /// <summary>
    /// Gets the ids of the agents that <c>--agent</c> accepts.
    /// </summary>
    /// <value>
    /// The agent ids in the order of <see cref="IIntegrationService.AgentIds" />, today <c>claude</c>,
    /// <c>copilot</c>, and <c>codex</c>.
    /// </value>
    public static IReadOnlyList<string> SupportedAgentIds { get; } =
    [
        ClaudeCodeIntegration.ClaudeAgentId, CopilotCliIntegration.CopilotAgentId, CodexCliIntegration.CodexAgentId
    ];

    /// <summary>
    /// Converts the specified value of <c>--agent</c> to the id of a supported agent.
    /// </summary>
    /// <param name="value">The value that follows <c>--agent</c>, or <see langword="null" /> if there is none.</param>
    /// <param name="targetType">The type of the option property.</param>
    /// <param name="optionAttribute">The attribute that declares the option.</param>
    /// <returns>
    /// The id from <see cref="SupportedAgentIds" /> that equals the value, compared case-insensitively.
    /// </returns>
    /// <exception cref="InvalidAgentOptionException">
    /// <paramref name="value" /> is <see langword="null" /> or no id of a supported agent.
    /// </exception>
    public object? Convert(object? value, Type targetType, OptionBaseAttribute optionAttribute)
    {
        var text = value?.ToString();

        return SupportedAgentIds.FirstOrDefault(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidAgentOptionException(text, SupportedAgentIds);
    }
}
