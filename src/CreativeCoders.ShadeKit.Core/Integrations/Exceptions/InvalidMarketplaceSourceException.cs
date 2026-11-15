namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the marketplace source that a developer set in the environment
/// variable <see cref="IntegrationService.MarketplaceSourceVariableName" /> cannot be used.
/// </summary>
/// <param name="value">The value of the environment variable.</param>
/// <param name="reason">A sentence that describes why the value cannot be used.</param>
public sealed class InvalidMarketplaceSourceException(string value, string reason)
    : Exception($"The marketplace source '{value}' cannot be used: {reason}")
{
    /// <summary>
    /// Gets the marketplace source that cannot be used.
    /// </summary>
    /// <value>The value of the environment variable as it was set.</value>
    public string Value { get; } = value;

    /// <summary>
    /// Gets the reason why the marketplace source cannot be used.
    /// </summary>
    /// <value>A sentence, for example <c>The value is empty.</c></value>
    public string Reason { get; } = reason;
}
