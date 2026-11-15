namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations.Install;

/// <summary>
/// Represents the exception that is thrown when <c>--agent</c> is given without a value or with an id that is not the
/// id of a supported agent.
/// </summary>
/// <remarks>
/// The option parser throws it while it reads the arguments of <c>shade-kit integrations install</c>, before the
/// command runs, so it reaches the CLI host as an argument error.
/// </remarks>
/// <param name="value">The value that follows <c>--agent</c>, or <see langword="null" /> if there is none.</param>
/// <param name="supportedAgentIds">The ids of the supported agents.</param>
public sealed class InvalidAgentOptionException(string? value, IReadOnlyList<string> supportedAgentIds)
    : Exception(value is null
        ? "The option '--agent' was given without an agent id."
        : $"The agent '{value}' of the option '--agent' is not supported.")
{
    /// <summary>
    /// Gets the value that follows <c>--agent</c>.
    /// </summary>
    /// <value>The value as passed, or <see langword="null" /> if <c>--agent</c> was given without a value.</value>
    public string? Value { get; } = value;

    /// <summary>
    /// Gets the ids of the supported agents.
    /// </summary>
    /// <value>The agent ids, for example <c>claude</c>.</value>
    public IReadOnlyList<string> SupportedAgentIds { get; } = supportedAgentIds;
}
