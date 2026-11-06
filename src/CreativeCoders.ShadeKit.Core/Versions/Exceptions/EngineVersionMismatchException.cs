namespace CreativeCoders.ShadeKit.Core.Versions.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the major or minor version of a release engine differs from that of
/// the expected version.
/// </summary>
/// <param name="engineVersion">The version of the running engine.</param>
/// <param name="expectedVersion">The expected version as it was passed to the check.</param>
/// <param name="direction">
/// One of the enumeration values that specifies whether the engine is older or newer than the expected version.
/// </param>
public class EngineVersionMismatchException(
    string engineVersion,
    string expectedVersion,
    EngineVersionMismatchDirection direction)
    : Exception($"The engine version '{engineVersion}' does not match the expected version '{expectedVersion}': the " +
                "major and minor versions differ.")
{
    /// <summary>
    /// Gets the version of the running engine.
    /// </summary>
    /// <value>The engine version without build metadata, for example <c>1.2.0</c>.</value>
    public string EngineVersion { get; } = engineVersion;

    /// <summary>
    /// Gets the expected version.
    /// </summary>
    /// <value>The version as it was passed to the check, for example the value of <c>--expect</c>.</value>
    public string ExpectedVersion { get; } = expectedVersion;

    /// <summary>
    /// Gets whether the engine is older or newer than the expected version.
    /// </summary>
    /// <value>One of the enumeration values.</value>
    public EngineVersionMismatchDirection Direction { get; } = direction;
}
