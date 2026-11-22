using System.Text.Json.Serialization;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit version --json</c> returns.
/// </summary>
/// <remarks>
/// Describes the installed engine and the SHADE kit integrations of the agents, not a repository; skills use it for
/// the version handshake. The properties are written in the order <c>engineVersion</c>, <c>formats</c>,
/// <c>contract</c>, <c>expectedContract</c>, <c>integrations</c>.
/// </remarks>
/// <param name="EngineVersion">The version of the running engine without build metadata.</param>
/// <param name="Formats">
/// The current format version of every versioned file type the engine supports, keyed by format id, for example
/// <c>config</c> or <c>current-feature</c>.
/// </param>
/// <param name="Contract">The window of CLI contracts that the engine supports.</param>
public sealed record VersionCommandData(
    string EngineVersion,
    IReadOnlyDictionary<string, int> Formats,
    VersionCommandContractData Contract)
{
    /// <summary>
    /// Gets the CLI contract that the kit skills are written for and that the engine was checked against.
    /// </summary>
    /// <value>
    /// The value of <c>--expect-contract</c>, or <see langword="null" /> if <c>--expect-contract</c> was not given, in
    /// which case the property is left out of the JSON output. The default is <see langword="null" />.
    /// </value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExpectedContract { get; init; }

    /// <summary>
    /// Gets the SHADE kit integrations that are installed in the agents for the user.
    /// </summary>
    /// <value>
    /// The installed integrations in the order in which the agents are registered, written to the JSON output after
    /// <see cref="ExpectedContract" />; an empty list if none is installed or no agent could be asked. It is
    /// <see langword="null" />, and left out of the JSON output, only when <c>--expect-contract</c> was given,
    /// because the command then does not read the integrations. The default is <see langword="null" />.
    /// </value>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<VersionCommandIntegrationData>? Integrations { get; init; }
}
