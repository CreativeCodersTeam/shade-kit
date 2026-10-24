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
    /// <summary>
    /// Gets the full path of the specified file and checks that it lies below the repository root.
    /// </summary>
    /// <param name="repositoryRootPath">The root directory of the repository.</param>
    /// <param name="relativePath">The path of the file, relative to <paramref name="repositoryRootPath" />.</param>
    /// <param name="paramName">
    /// The name of the caller's parameter that holds <paramref name="relativePath" />, which an
    /// <see cref="ArgumentException" /> for an invalid <paramref name="relativePath" /> reports as its
    /// <see cref="ArgumentException.ParamName" />. The compiler fills it in from the argument expression.
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
        return Resolve(repositoryRootPath, relativePath, paramName).FullPath;
    }

    /// <summary>
    /// Gets the path of the specified file in the form that <c>git</c> expects for a path relative to the repository
    /// root.
    /// </summary>
    /// <param name="repositoryRootPath">The root directory of the repository.</param>
    /// <param name="relativePath">The path of the file, relative to <paramref name="repositoryRootPath" />.</param>
    /// <param name="paramName">
    /// The name of the caller's parameter that holds <paramref name="relativePath" />, which an
    /// <see cref="ArgumentException" /> for an invalid <paramref name="relativePath" /> reports as its
    /// <see cref="ArgumentException.ParamName" />. The compiler fills it in from the argument expression.
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
        var normalizedRelativePath = Resolve(repositoryRootPath, relativePath, paramName)
            .NormalizedRelativePath;

        return Path.DirectorySeparatorChar == '/'
            ? normalizedRelativePath
            : normalizedRelativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    [SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly",
        Justification = "The caller's parameter name is forwarded on purpose, so an invalid path names the caller's " +
                        "parameter instead of this helper's.")]
    private static (string FullPath, string NormalizedRelativePath) Resolve(string repositoryRootPath,
        string relativePath, string paramName)
    {
        Ensure.IsNotNullOrWhitespace(repositoryRootPath);
        Ensure.IsNotNullOrWhitespace(relativePath, paramName);

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException(
                $"The path '{relativePath}' must be relative to the repository root, but it is rooted.",
                paramName);
        }

        var fullRootPath = Path.GetFullPath(repositoryRootPath);
        var fullPath = Path.GetFullPath(Path.Combine(fullRootPath, relativePath));
        var normalizedRelativePath = Path.GetRelativePath(fullRootPath, fullPath);

        if (normalizedRelativePath == "." || IsOutsideRoot(normalizedRelativePath))
        {
            throw new ArgumentException(
                $"The path '{relativePath}' must name a path below the repository root '{repositoryRootPath}'.",
                paramName);
        }

        return (fullPath, normalizedRelativePath);
    }

    private static bool IsOutsideRoot(string normalizedRelativePath)
    {
        return normalizedRelativePath == ".."
               || normalizedRelativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
               || Path.IsPathRooted(normalizedRelativePath);
    }
}
