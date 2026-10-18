using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Init;

namespace CreativeCoders.ShadeKit.Cli.Commands.Init;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit init --json</c> returns.
/// </summary>
/// <param name="RepositoryRoot">The full path of the root directory of the initialised git repository.</param>
/// <param name="Files">
/// The files and directories that were created or changed, in the order in which they were written.
/// </param>
public sealed record InitCommandData(string RepositoryRoot, IReadOnlyList<InitCommandFile> Files)
{
    /// <summary>
    /// Creates the data from the result of a repository initialisation.
    /// </summary>
    /// <param name="result">The result of the initialisation.</param>
    /// <returns>
    /// The data with the repository root and the files of <paramref name="result" /> in their original order, each with
    /// its action as a lower-case kebab-case name.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A file in <paramref name="result" /> has an action that is not defined.
    /// </exception>
    public static InitCommandData FromResult(RepositoryInitResult result)
    {
        Ensure.NotNull(result);

        return new InitCommandData(result.RepositoryRootPath,
            [.. result.Files.Select(x => new InitCommandFile(x.Path, ToActionName(x.Action)))]);
    }

    private static string ToActionName(InitializedFileAction action)
    {
        // The names are part of the JSON contract, so they are spelled out instead of derived from the enum members.
        return action switch
        {
            InitializedFileAction.Created => "created",
            InitializedFileAction.Appended => "appended",
            InitializedFileAction.Updated => "updated",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "The file action is not supported.")
        };
    }
}
