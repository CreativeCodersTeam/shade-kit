using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents the configured gate of the forward transition into a phase, together with the location of the
/// feature's copy of its checklist and whether that copy exists.
/// </summary>
/// <remarks>
/// The gate is <see cref="TransitionGate.ForPhase" /> of the gates that <see cref="ITransitionGateProvider" />
/// configures for the profile. It does not depend on the state of the phase: neither a pending transition nor a reopen
/// of the phase is considered.
/// </remarks>
/// <param name="Gate">The target phase and its configured gate.</param>
/// <param name="ChecklistPath">
/// The path of the feature's copy of the checklist relative to the repository root, with <c>/</c> as separator, as
/// <see cref="KitStorePaths.GetFeatureChecklistPath" /> builds it, for example
/// <c>.shadekit/features/gh-142-login-sso/checks/definition-of-ready.md</c>; or <see langword="null" /> if the gate
/// has no checklist.
/// </param>
/// <param name="ChecklistExists">
/// <see langword="true" /> if the feature's copy of the checklist exists as a file; <see langword="false" /> if it does
/// not, also if a directory exists in its place; or <see langword="null" /> if the gate has no checklist.
/// </param>
public sealed record GateInfoGate(PhaseGate Gate, string? ChecklistPath, bool? ChecklistExists);
