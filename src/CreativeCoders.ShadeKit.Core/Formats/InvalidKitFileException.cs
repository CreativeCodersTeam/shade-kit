namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Represents the exception that is thrown when a file of the SHADE kit store cannot be used, for example because a
/// versioned file is not valid JSON or has no <c>schemaVersion</c>, or because a template is not UTF-8 text.
/// </summary>
/// <param name="filePath">The path of the file.</param>
/// <param name="reason">A sentence that describes why the file cannot be used.</param>
/// <param name="innerException">The exception that caused this exception, or <see langword="null" /> if there is none.</param>
public class InvalidKitFileException(string filePath, string reason, Exception? innerException = null)
    : KitFileFormatException(filePath, $"The file '{filePath}' is invalid: {reason}", innerException)
{
    /// <summary>
    /// Gets the reason why the file cannot be used.
    /// </summary>
    /// <value>A sentence that describes the problem, for example that the <c>schemaVersion</c> field is missing.</value>
    public string Reason { get; } = reason;
}
