namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the exception that is thrown when no usable feature title is available, for example because a URL
/// source has no title, the title contains a control character, or the title contains no letter or digit from which
/// a slug can be built.
/// </summary>
/// <param name="reason">A sentence that describes why no usable title is available.</param>
public class InvalidFeatureTitleException(string reason)
    : Exception($"The feature title cannot be used: {reason}")
{
    /// <summary>
    /// Gets the reason why no usable title is available.
    /// </summary>
    /// <value>
    /// A sentence that describes the problem and names the title that was used, if there was one and it can be shown
    /// on one line.
    /// </value>
    public string Reason { get; } = reason;
}
