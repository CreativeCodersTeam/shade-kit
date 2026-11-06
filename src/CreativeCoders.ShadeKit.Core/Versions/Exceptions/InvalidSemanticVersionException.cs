namespace CreativeCoders.ShadeKit.Core.Versions.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a version that a caller passes is not a valid SemVer 2.0.0 version.
/// </summary>
/// <param name="value">The version text that is not valid.</param>
public class InvalidSemanticVersionException(string value)
    : Exception($"'{value}' is not a valid SemVer 2.0.0 version.")
{
    /// <summary>
    /// Gets the version text that is not valid.
    /// </summary>
    /// <value>The text as it was passed, for example the value of <c>--expect</c>.</value>
    public string Value { get; } = value;
}
