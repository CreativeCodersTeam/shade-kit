namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Drop;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit feature drop &lt;id&gt; --json</c> returns.
/// </summary>
/// <param name="Id">The id of the feature.</param>
/// <param name="Dropped">
/// <see langword="true" /> if the feature folder was deleted; <see langword="false" /> if the command only described
/// what it would delete.
/// </param>
/// <param name="Files">
/// The repository-relative paths of every file in the feature folder, which were deleted or would be deleted.
/// </param>
/// <param name="OverrideAffected">
/// <see langword="true" /> if the local override points to the feature, so that dropping it clears or would clear the
/// override; otherwise, <see langword="false" />. It is set in the description and after a drop alike.
/// </param>
/// <param name="OverrideCleared">
/// <see langword="true" /> if the local override pointed to the feature and was deleted; otherwise,
/// <see langword="false" />, also when the command only described what it would delete.
/// </param>
public sealed record FeatureDropCommandData(
    string Id,
    bool Dropped,
    IReadOnlyList<string> Files,
    bool OverrideAffected,
    bool OverrideCleared);
