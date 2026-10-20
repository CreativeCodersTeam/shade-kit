using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Init;

namespace CreativeCoders.ShadeKit.Cli.Commands.Init;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit init --json</c> returns.
/// </summary>
/// <param name="RepositoryRoot">The full path of the root directory of the initialised git repository.</param>
/// <param name="Files">
/// The files and directories that were created or changed, in the order in which they were written. Each action is
/// written as <c>created</c>, <c>appended</c>, or <c>updated</c>.
/// </param>
public sealed record InitCommandData(string RepositoryRoot, IReadOnlyList<InitializedFile> Files)
{
    /// <summary>
    /// Creates the data from the result of a repository initialisation.
    /// </summary>
    /// <param name="result">The result of the initialisation.</param>
    /// <returns>The data with the repository root and the files of <paramref name="result" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    public static InitCommandData FromResult(RepositoryInitResult result)
    {
        Ensure.NotNull(result);

        return new InitCommandData(result.RepositoryRootPath, result.Files);
    }
}
