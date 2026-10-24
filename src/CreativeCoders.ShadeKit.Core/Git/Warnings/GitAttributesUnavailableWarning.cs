using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Git.Warnings;

/// <summary>
/// Represents a warning that <c>git</c> is installed but failed to read the git attributes or the git configuration
/// of a repository, so git did not decide the line endings of its files.
/// </summary>
/// <remarks>
/// For example, <c>git</c> refuses a repository that is owned by another user (<c>safe.directory</c>). The line
/// endings then come from the <c>.editorconfig</c> files or the operating system. A missing <c>git</c> gives no
/// warning.
/// </remarks>
/// <param name="RepositoryRootPath">The absolute path of the repository root.</param>
/// <param name="Command">
/// The <c>git</c> command that failed, without its arguments, for example <c>git check-attr</c> or <c>git config</c>.
/// </param>
/// <param name="Reason">A sentence that describes how the command failed.</param>
public sealed record GitAttributesUnavailableWarning(string RepositoryRootPath, string Command, string Reason)
    : KitWarning;
