namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Represents the base class of the exceptions that stop a command because of the format of a versioned file.
/// </summary>
public abstract class KitFileFormatException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KitFileFormatException" /> class.
    /// </summary>
    /// <param name="filePath">The path of the file whose format stops the command.</param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception, or <see langword="null" /> if there is none.</param>
    protected KitFileFormatException(string filePath, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Gets the path of the file whose format stops the command.
    /// </summary>
    /// <value>The path of the file as it was passed to the reader.</value>
    public string FilePath { get; }
}
