namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Specifies what an install or update did with the SHADE kit integration of an agent.
/// </summary>
public enum IntegrationOutcome
{
    /// <summary>
    /// The integration was not installed before and is installed now.
    /// </summary>
    Installed,

    /// <summary>
    /// The integration was installed before and was updated or installed again from the targeted source.
    /// </summary>
    Updated,

    /// <summary>
    /// The integration was installed from the targeted source already and had the latest version; nothing was changed.
    /// </summary>
    Current
}
