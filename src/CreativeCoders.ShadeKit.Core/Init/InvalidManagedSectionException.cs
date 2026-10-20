namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Represents the exception that is thrown when the markers of the managed SHADE kit section in an instruction file
/// are invalid, so the section cannot be located safely.
/// </summary>
/// <param name="filePath">The path of the instruction file.</param>
/// <param name="reason">A sentence that describes why the markers are invalid.</param>
public class InvalidManagedSectionException(string filePath, string reason)
    : Exception($"The managed SHADE kit section in '{filePath}' is invalid: {reason}")
{
    /// <summary>
    /// Gets the path of the instruction file whose markers are invalid.
    /// </summary>
    /// <value>The path of the file as it was passed to the planner.</value>
    public string FilePath { get; } = filePath;

    /// <summary>
    /// Gets the reason why the markers are invalid.
    /// </summary>
    /// <value>A sentence that describes the problem, for example that a begin marker has no end marker.</value>
    public string Reason { get; } = reason;
}
