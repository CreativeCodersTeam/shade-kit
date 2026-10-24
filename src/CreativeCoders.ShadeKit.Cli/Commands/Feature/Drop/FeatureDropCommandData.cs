namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Drop;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit feature drop &lt;id&gt; --json</c> returns.
/// </summary>
/// <param name="Id">The id of the feature.</param>
/// <param name="Dropped">
/// <see langword="true" /> if the feature folder was deleted; <see langword="false" /> if the drop was only
/// described.
/// </param>
/// <param name="Files">The repository-relative paths of every file in the feature folder.</param>
/// <param name="OverrideAffected">
/// <see langword="true" /> if the local override selects the feature, in a description and after a drop alike.
/// </param>
/// <param name="OverrideCleared">
/// <see langword="true" /> if the local override was deleted; always <see langword="false" /> for a description.
/// </param>
public sealed record FeatureDropCommandData(
    string Id,
    bool Dropped,
    IReadOnlyList<string> Files,
    bool OverrideAffected,
    bool OverrideCleared);
