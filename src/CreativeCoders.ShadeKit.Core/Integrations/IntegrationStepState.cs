namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Records how far the steps of one install or update of an agent integration got, so that a failure can be
/// reported by what was already changed.
/// </summary>
/// <remarks>
/// An agent integration passes one instance to its steps through
/// <see cref="AgentIntegrationBase{TPlugin}.RunStepsAsync" />, which reads it at the time of a failure.
/// </remarks>
internal sealed class IntegrationStepState
{
    /// <summary>
    /// Gets a value that indicates whether the steps switch the marketplace to another source.
    /// </summary>
    /// <value>
    /// <see langword="true" /> from the call of <see cref="StartSwitch" /> on; otherwise, <see langword="false" />.
    /// </value>
    public bool IsSwitching { get; private set; }

    /// <summary>
    /// Gets a value that indicates whether a step removed the plugin or the marketplace.
    /// </summary>
    /// <value>
    /// <see langword="true" /> from the call of <see cref="MarkRemoved" /> on; otherwise, <see langword="false" />.
    /// </value>
    public bool IsRemoved { get; private set; }

    /// <summary>
    /// Records that the steps start to switch the marketplace.
    /// </summary>
    /// <remarks>
    /// Called right before the first removal, so that a cancellation while that removal runs counts as a canceled
    /// switch: the killed agent CLI may have removed it.
    /// </remarks>
    public void StartSwitch()
    {
        IsSwitching = true;
    }

    /// <summary>
    /// Records that a step removed the plugin or the marketplace.
    /// </summary>
    /// <remarks>Called right after the removal succeeded.</remarks>
    public void MarkRemoved()
    {
        IsRemoved = true;
    }
}
