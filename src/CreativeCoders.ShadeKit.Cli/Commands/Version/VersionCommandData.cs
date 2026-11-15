using System.Text.Json.Serialization;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit version --json</c> returns.
/// </summary>
/// <remarks>
/// Describes the installed engine and the SHADE kit integrations of the agents, not a repository; skills use it for
/// the version handshake.
/// </remarks>
/// <param name="EngineVersion">The version of the running engine without build metadata.</param>
/// <param name="Formats">
/// The current format version of every versioned file type the engine supports, keyed by format id, for example
/// <c>config</c> or <c>current-feature</c>.
/// </param>
public sealed record VersionCommandData(string EngineVersion, IReadOnlyDictionary<string, int> Formats)
{
    /// <summary>
    /// Gets the engine version that the kit skills expect and that the engine was checked against.
    /// </summary>
    /// <value>
    /// The value of <c>--expect</c> as passed, or <see langword="null" /> if <c>--expect</c> was not given, in which
    /// case the property is left out of the JSON output. The default is <see langword="null" />.
    /// </value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpectedVersion { get; init; }

    /// <summary>
    /// Gets the SHADE kit integrations that are installed in the agents for the user.
    /// </summary>
    /// <value>
    /// The installed integrations in the order in which the agents are registered, always written to the JSON output,
    /// after <see cref="ExpectedVersion" />; an empty list if none is installed or no agent could be asked. The
    /// default is an empty list.
    /// </value>
    public IReadOnlyList<VersionCommandIntegrationData> Integrations { get; init; } = [];
}
