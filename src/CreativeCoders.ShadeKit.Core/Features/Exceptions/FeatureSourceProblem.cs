namespace CreativeCoders.ShadeKit.Core.Features.Exceptions;

/// <summary>
/// Specifies why the source of a new feature cannot be used, so that callers can suggest how to fix it.
/// </summary>
public enum FeatureSourceProblem
{
    /// <summary>
    /// The source itself cannot be used, for example because it is empty, is a tracker URL without an issue number,
    /// or names a file that cannot be read, is too large, or is not UTF-8 text.
    /// </summary>
    InvalidSource,

    /// <summary>
    /// The source is a tracker URL, but the GitHub CLI <c>gh</c> that reads its issue cannot be found or started.
    /// </summary>
    GhUnavailable,

    /// <summary>
    /// The source is a tracker URL whose issue cannot be read, for example because <c>gh</c> fails or returns data
    /// that cannot be used.
    /// </summary>
    IssueReadFailed,

    /// <summary>
    /// The source is a URL whose host is not a tracker, and its content cannot be read.
    /// </summary>
    UrlReadFailed
}
