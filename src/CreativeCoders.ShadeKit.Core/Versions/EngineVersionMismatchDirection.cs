namespace CreativeCoders.ShadeKit.Core.Versions;

/// <summary>
/// Specifies how the major and minor versions of the running engine relate to those of the expected version.
/// </summary>
public enum EngineVersionMismatchDirection
{
    /// <summary>
    /// The major and minor versions of the engine are lower than those of the expected version, so the engine is
    /// older than the kit skills.
    /// </summary>
    EngineOlder,

    /// <summary>
    /// The major and minor versions of the engine are higher than those of the expected version, so the engine is
    /// newer than the kit skills.
    /// </summary>
    EngineNewer
}
