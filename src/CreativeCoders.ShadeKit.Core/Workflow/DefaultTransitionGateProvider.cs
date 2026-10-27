using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Provides <see cref="TransitionGate.Default" /> for every transition: each one needs an approval and has no
/// checklist.
/// </summary>
/// <remarks>
/// Reads no file. Package 05 replaces this provider with one that reads the gates from <c>gates.json</c>.
/// </remarks>
public sealed class DefaultTransitionGateProvider : ITransitionGateProvider
{
    /// <inheritdoc />
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result is always
    /// <see cref="TransitionGate.Default" />.
    /// </returns>
    public Task<TransitionGate> GetGateAsync(IKitStore store, WorkflowProfile profile, string targetPhase,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(profile);
        Ensure.NotNull(targetPhase);

        return Task.FromResult(TransitionGate.Default);
    }
}
