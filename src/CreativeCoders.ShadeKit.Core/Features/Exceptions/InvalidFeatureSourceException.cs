namespace CreativeCoders.ShadeKit.Core.Features.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the source from which a feature is created cannot be used, for
/// example because it is empty, is an issue URL of a known tracker without an issue number or whose issue cannot be
/// read with <c>gh</c>, is another URL whose content cannot be read, or names a file that cannot be read, is larger
/// than 1 MiB, or is not UTF-8 text.
/// </summary>
/// <param name="source">The source as it was passed, for example the value of <c>--source</c>.</param>
/// <param name="reason">A sentence that describes why the source cannot be used.</param>
/// <param name="innerException">The exception that caused this exception, or <see langword="null" /> if there is none.</param>
public class InvalidFeatureSourceException(string source, string reason, Exception? innerException = null)
    : Exception($"The feature source '{source}' cannot be used: {reason}", innerException)
{
    /// <summary>
    /// Gets the source that cannot be used.
    /// </summary>
    /// <remarks>
    /// The property is not named <c>Source</c>, because <see cref="Exception.Source" /> names the application or
    /// object that causes the error.
    /// </remarks>
    /// <value>The source as it was passed, for example the value of <c>--source</c>.</value>
    public string SourceText { get; } = source;

    /// <summary>
    /// Gets the reason why the source cannot be used.
    /// </summary>
    /// <value>A sentence that describes the problem, for example that the URL contains no issue number.</value>
    public string Reason { get; } = reason;
}
