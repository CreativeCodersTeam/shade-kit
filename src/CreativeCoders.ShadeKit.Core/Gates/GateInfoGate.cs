using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Represents the configured gate of the forward transition into a phase, together with the location of the
/// feature's copy of its checklist and whether that copy exists.
/// </summary>
/// <remarks>
/// The gate is the one that <see cref="ITransitionGateProvider" /> configures for the phase, or
/// <see cref="TransitionGate.Default" /> if <c>gates.json</c> has no entry for it. It does not depend on the state of
/// the phase, and a reopen of the phase is not considered.
/// </remarks>
/// <param name="Phase">The name of the target phase of the transition, for example <c>plan</c>.</param>
/// <param name="Approval">
/// <see langword="true" /> if a human must approve the transition into <paramref name="Phase" />;
/// <see langword="false" /> if the transition is performed at once.
/// </param>
/// <param name="Checklist">
/// The path of the checklist of the gate as configured in <c>gates.json</c>, relative to the feature folder and to
/// <c>.shadekit/templates/</c>, for example <c>checks/definition-of-ready.md</c>; or <see langword="null" /> if the
/// gate has no checklist.
/// </param>
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
public sealed record GateInfoGate(
    string Phase,
    bool Approval,
    string? Checklist,
    string? ChecklistPath,
    bool? ChecklistExists);
