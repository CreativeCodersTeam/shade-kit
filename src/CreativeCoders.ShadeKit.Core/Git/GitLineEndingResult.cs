using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Git.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Represents the line ending that the git checkout configuration prescribes for a file, together with the warning
/// of a <c>git</c> that failed.
/// </summary>
/// <param name="LineEnding">
/// The line ending that git prescribes, or <see langword="null" /> if git does not decide.
/// </param>
/// <param name="Warning">
/// A <see cref="GitAttributesUnavailableWarning" /> if <c>git</c> is installed but failed, in which case
/// <paramref name="LineEnding" /> is <see langword="null" />; otherwise, <see langword="null" />.
/// </param>
public sealed record GitLineEndingResult(LineEnding? LineEnding, KitWarning? Warning);
