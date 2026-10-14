namespace CreativeCoders.ShadeKit.Core.Engine;

/// <summary>
/// Provides the version of the running SHADE kit engine.
/// </summary>
public interface IEngineVersionProvider
{
    /// <summary>
    /// Gets the version of the running engine.
    /// </summary>
    /// <value>The engine version without build metadata, for example <c>1.2.0</c> or <c>1.3.0-beta.1</c>.</value>
    string Version { get; }
}
