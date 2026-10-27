using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents the phase fields of a <c>feature.json</c> file as they are stored, before
/// <see cref="IPhaseEngine.ReadState" /> validates them against the workflow profile.
/// </summary>
/// <param name="FeatureId">The id of the feature, for example <c>gh-142-login-sso</c>.</param>
/// <param name="FeatureFilePath">
/// The repository-relative path of the <c>feature.json</c> file, which messages name.
/// </param>
/// <param name="Status">The stored <see cref="FeatureDocument.Status" />.</param>
/// <param name="CurrentPhase">
/// The stored <see cref="FeatureDocument.CurrentPhase" />, or <see langword="null" /> if the field is missing.
/// </param>
/// <param name="Phases">
/// The stored <see cref="FeatureDocument.Phases" />, enumerated in the order they were read, or
/// <see langword="null" /> if the field is missing.
/// </param>
/// <param name="PendingTransition">
/// The stored <see cref="FeatureDocument.PendingTransition" />, or <see langword="null" /> if the field is missing.
/// </param>
public sealed record StoredPhaseState(
    string FeatureId,
    string FeatureFilePath,
    FeatureStatus Status,
    string? CurrentPhase,
    IReadOnlyDictionary<string, PhaseState>? Phases,
    string? PendingTransition);
