using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Processes;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Git;

/// <summary>
/// Configures fakes of <see cref="IGitCommandRunner" />.
/// </summary>
internal static class GitCommandRunnerFakeExtensions
{
    /// <summary>
    /// Makes the fake return the specified result for a <c>git</c> run with exactly the specified arguments, in any
    /// working directory.
    /// </summary>
    public static void SetUpResult(this IGitCommandRunner gitCommandRunner, IReadOnlyList<string> arguments,
        ExternalProcessResult result)
    {
        A.CallTo(() => gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(arguments), A<CancellationToken>._))
            .Returns(result);
    }
}
