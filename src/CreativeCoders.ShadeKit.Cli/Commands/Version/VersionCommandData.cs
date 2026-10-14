namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit version --json</c> returns.
/// </summary>
/// <remarks>
/// The data describes the capabilities of the installed engine, not the state of a repository. Skills use it for the
/// version handshake.
/// </remarks>
/// <param name="EngineVersion">The version of the running engine without build metadata.</param>
/// <param name="Formats">
/// The current format version of every versioned file type the engine supports, keyed by format id, for example
/// <c>config</c> or <c>current-feature</c>.
/// </param>
public sealed record VersionCommandData(string EngineVersion, IReadOnlyDictionary<string, int> Formats);
