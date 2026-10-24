using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Combines the root directory of a repository with the path of a file relative to it.
/// </summary>
/// <remarks>
/// A relative path may use forward slashes or the directory separator of the operating system, and it may contain
/// <c>.</c> and <c>..</c> segments as long as it ends below the repository root.
/// </remarks>
internal static class RepositoryFilePath
{
    private const string RepositoryRootDescription = "the repository root";

    /// <summary>
    /// Gets the full path of the specified file and checks that it lies below the repository root.
    /// </summary>
    /// <param name="repositoryRootPath">The root directory of the repository.</param>
    /// <param name="relativePath">The path of the file, relative to <paramref name="repositoryRootPath" />.</param>
    /// <param name="paramName">
    /// The name of the caller's parameter for <paramref name="relativePath" />, reported by an
    /// <see cref="ArgumentException" />; the compiler fills it in.
    /// </param>
    /// <returns>The absolute, normalized path of the file.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repositoryRootPath" /> or <paramref name="relativePath" /> is <see langword="null" />, empty,
    /// or whitespace, or <paramref name="relativePath" /> is rooted or does not name a path below
    /// <paramref name="repositoryRootPath" />.
    /// </exception>
    public static string GetFullPath(string repositoryRootPath, string relativePath,
        [CallerArgumentExpression(nameof(relativePath))] string paramName = "")
    {
        Ensure.IsNotNullOrWhitespace(repositoryRootPath);

        return Resolve(repositoryRootPath, relativePath, RepositoryRootDescription, paramName).FullPath;
    }

    /// <summary>
    /// Gets the full path of the specified file and checks that it lies below the specified root directory, which
    /// the message of an <see cref="ArgumentException" /> names with the specified description.
    /// </summary>
    /// <param name="rootPath">The root directory, for example the SHADE kit root.</param>
    /// <param name="relativePath">The path of the file, relative to <paramref name="rootPath" />.</param>
    /// <param name="rootDescription">The description of the root directory, for example <c>the SHADE kit root</c>.</param>
    /// <param name="paramName">
    /// The name of the caller's parameter for <paramref name="relativePath" />, reported by an
    /// <see cref="ArgumentException" />.
    /// </param>
    /// <returns>The absolute, normalized path of the file.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="rootPath" /> or <paramref name="relativePath" /> is <see langword="null" />, empty, or
    /// whitespace, or <paramref name="relativePath" /> is rooted or does not name a path below
    /// <paramref name="rootPath" />.
    /// </exception>
    public static string GetFullPathBelowRoot(string rootPath, string relativePath, string rootDescription,
        string paramName)
    {
        return Resolve(rootPath, relativePath, rootDescription, paramName).FullPath;
    }

    /// <summary>
    /// Gets the path of the specified file in the form that <c>git</c> expects for a path relative to the repository
    /// root.
    /// </summary>
    /// <param name="repositoryRootPath">The root directory of the repository.</param>
    /// <param name="relativePath">The path of the file, relative to <paramref name="repositoryRootPath" />.</param>
    /// <param name="paramName">
    /// The name of the caller's parameter for <paramref name="relativePath" />, reported by an
    /// <see cref="ArgumentException" />; the compiler fills it in.
    /// </param>
    /// <returns>
    /// The normalized path of the file relative to <paramref name="repositoryRootPath" />, with forward slashes as
    /// separators, for example <c>docs/readme.md</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repositoryRootPath" /> or <paramref name="relativePath" /> is <see langword="null" />, empty,
    /// or whitespace, or <paramref name="relativePath" /> is rooted or does not name a path below
    /// <paramref name="repositoryRootPath" />.
    /// </exception>
    public static string GetGitPath(string repositoryRootPath, string relativePath,
        [CallerArgumentExpression(nameof(relativePath))] string paramName = "")
    {
        Ensure.IsNotNullOrWhitespace(repositoryRootPath);

        return ToForwardSlashes(Resolve(repositoryRootPath, relativePath, RepositoryRootDescription, paramName)
            .NormalizedRelativePath);
    }

    /// <summary>
    /// Replaces every directory separator of the operating system in the specified path with a forward slash, the
    /// separator of git paths and of the paths that SHADE kit reports.
    /// </summary>
    /// <param name="path">The path with the directory separator of the operating system.</param>
    /// <returns><paramref name="path" /> with forward slashes as separators.</returns>
    public static string ToForwardSlashes(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Determines whether a path that <see cref="Path.GetRelativePath" /> returned for a root directory and a full path
    /// leads out of the root directory.
    /// </summary>
    /// <param name="normalizedRelativePath">The relative path that <see cref="Path.GetRelativePath" /> returned.</param>
    /// <returns>
    /// <see langword="true" /> if the path is <c>..</c>, starts with <c>..</c> and a directory separator, or is rooted
    /// because the full path lies on another drive; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsOutsideRoot(string normalizedRelativePath)
    {
        return normalizedRelativePath == ".."
               || normalizedRelativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
               || Path.IsPathRooted(normalizedRelativePath);
    }

    [SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly",
        Justification = "The caller's parameter name is forwarded on purpose, so an invalid path names the caller's " +
                        "parameter instead of this helper's.")]
    private static (string FullPath, string NormalizedRelativePath) Resolve(string rootPath, string relativePath,
        string rootDescription, string paramName)
    {
        Ensure.IsNotNullOrWhitespace(rootPath);
        Ensure.IsNotNullOrWhitespace(relativePath, paramName);

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException(
                $"The path '{relativePath}' must be relative to {rootDescription}, but it is rooted.", paramName);
        }

        var fullRootPath = Path.GetFullPath(rootPath);
        var fullPath = Path.GetFullPath(Path.Combine(fullRootPath, relativePath));
        var normalizedRelativePath = Path.GetRelativePath(fullRootPath, fullPath);

        if (normalizedRelativePath == "." || IsOutsideRoot(normalizedRelativePath))
        {
            throw new ArgumentException(
                $"The path '{relativePath}' must name a path below {rootDescription} '{rootPath}'.", paramName);
        }

        return (fullPath, normalizedRelativePath);
    }
}
