namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Specifies whether a tag exists in a remote git repository, as an <see cref="IGitRemoteTagChecker" /> found out.
/// </summary>
public enum GitRemoteTagState
{
    /// <summary>
    /// The remote repository has the tag.
    /// </summary>
    Found,

    /// <summary>
    /// The remote repository was read and has no tag of that name.
    /// </summary>
    Missing,

    /// <summary>
    /// The remote repository could not be read, for example because <c>git</c> is not installed, the network is not
    /// available, or the repository needs credentials, so it is not known whether the tag exists.
    /// </summary>
    Unknown
}
