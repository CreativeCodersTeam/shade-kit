using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Git.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the <c>git</c> executable cannot be started or a <c>git</c> command
/// fails in a way that SHADE kit does not expect.
/// </summary>
/// <param name="arguments">The arguments of the <c>git</c> command that could not be run or failed.</param>
/// <param name="reason">A sentence that describes why the command could not be run or how it failed.</param>
/// <param name="innerException">The exception that caused this exception, or <see langword="null" /> if there is none.</param>
public class GitUnavailableException(
    IReadOnlyList<string> arguments,
    string reason,
    Exception? innerException = null)
    : ExternalToolUnavailableException("git", "failed", arguments, reason, innerException);
