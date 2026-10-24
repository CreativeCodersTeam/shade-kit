using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Represents a file in a repository whose path has been checked to lie below the repository root.
/// </summary>
/// <remarks>
/// The path is checked and normalized once, when the instance is created, so the writer and the line ending resolvers
/// that get an instance do not check it again. The file need not exist.
/// </remarks>
public sealed record RepositoryFile
{
    private const string RepositoryRootDescription = "the repository root";

    private RepositoryFile(string repositoryRootPath, string fullPath, string relativePath)
    {
        RepositoryRootPath = repositoryRootPath;
        FullPath = fullPath;
        RelativePath = relativePath;
    }

    /// <summary>
    /// Gets the root directory of the repository.
    /// </summary>
    /// <value>The absolute, normalized path of the repository root, without a trailing directory separator.</value>
    public string RepositoryRootPath { get; }

    /// <summary>
    /// Gets the full path of the file.
    /// </summary>
    /// <value>The absolute, normalized path of the file.</value>
    public string FullPath { get; }

    /// <summary>
    /// Gets the path of the file relative to the repository root.
    /// </summary>
    /// <value>
    /// The normalized path relative to <see cref="RepositoryRootPath" />, without <c>.</c> and <c>..</c> segments, with
    /// the directory separator of the operating system.
    /// </value>
    public string RelativePath { get; }

    /// <summary>
    /// Gets the path of the file in the form that <c>git</c> expects for a path relative to the repository root.
    /// </summary>
    /// <value><see cref="RelativePath" /> with forward slashes as separators, for example <c>docs/readme.md</c>.</value>
    public string GitPath => RepositoryFilePath.ToForwardSlashes(RelativePath);

    /// <summary>
    /// Creates the file with the specified path relative to the repository root.
    /// </summary>
    /// <remarks>
    /// The relative path may use forward slashes or the directory separator of the operating system, and it may
    /// contain <c>.</c> and <c>..</c> segments as long as it ends below the repository root.
    /// </remarks>
    /// <param name="repositoryRootPath">The root directory of the repository.</param>
    /// <param name="relativePath">The path of the file, relative to <paramref name="repositoryRootPath" />.</param>
    /// <param name="paramName">
    /// The name of the caller's parameter for <paramref name="relativePath" />, reported by an
    /// <see cref="ArgumentException" />; the compiler fills it in.
    /// </param>
    /// <returns>The file with its checked and normalized paths.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repositoryRootPath" /> or <paramref name="relativePath" /> is <see langword="null" />, empty,
    /// or whitespace, or <paramref name="relativePath" /> is rooted or does not name a path below
    /// <paramref name="repositoryRootPath" />.
    /// </exception>
    [SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly",
        Justification = "The caller's parameter name is forwarded on purpose, so an invalid path names the caller's " +
                        "parameter instead of this helper's.")]
    public static RepositoryFile Resolve(string repositoryRootPath, string relativePath,
        [CallerArgumentExpression(nameof(relativePath))] string paramName = "")
    {
        Ensure.IsNotNullOrWhitespace(repositoryRootPath);

        var (fullPath, normalizedRelativePath) = RepositoryFilePath.Resolve(repositoryRootPath, relativePath,
            RepositoryRootDescription, paramName);

        return new RepositoryFile(NormalizeRootPath(repositoryRootPath), fullPath, normalizedRelativePath);
    }

    /// <summary>
    /// Creates the file with the specified full path, which the caller has already checked to lie below the
    /// repository root.
    /// </summary>
    /// <param name="repositoryRootPath">The absolute path of the repository root.</param>
    /// <param name="fullPath">The absolute, normalized path of a file below <paramref name="repositoryRootPath" />.</param>
    /// <returns>The file with the specified paths.</returns>
    internal static RepositoryFile FromCheckedFullPath(string repositoryRootPath, string fullPath)
    {
        var rootPath = NormalizeRootPath(repositoryRootPath);

        return new RepositoryFile(rootPath, fullPath, Path.GetRelativePath(rootPath, fullPath));
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return GitPath;
    }

    // Spellings of the same root, such as a trailing separator, give the same path.
    private static string NormalizeRootPath(string repositoryRootPath)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRootPath));
    }
}
