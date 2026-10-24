using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Git;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Determines the line ending with which SHADE kit writes a file from the git checkout configuration, the
/// <c>.editorconfig</c> files, and the operating system, in this order.
/// </summary>
public sealed class LineEndingResolver : ILineEndingResolver
{
    private readonly IGitLineEndingResolver _gitLineEndingResolver;

    private readonly IEditorConfigLineEndingResolver _editorConfigLineEndingResolver;

    private readonly LineEnding _operatingSystemLineEnding;

    /// <summary>
    /// Initializes a new instance of the <see cref="LineEndingResolver" /> class that falls back to the line ending
    /// of the current operating system.
    /// </summary>
    /// <param name="gitLineEndingResolver">The resolver that reads the git checkout configuration.</param>
    /// <param name="editorConfigLineEndingResolver">The resolver that reads the <c>.editorconfig</c> files.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="gitLineEndingResolver" /> or <paramref name="editorConfigLineEndingResolver" /> is
    /// <see langword="null" />.
    /// </exception>
    public LineEndingResolver(IGitLineEndingResolver gitLineEndingResolver,
        IEditorConfigLineEndingResolver editorConfigLineEndingResolver)
        : this(gitLineEndingResolver, editorConfigLineEndingResolver, OperatingSystemLineEnding.Current)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LineEndingResolver" /> class with the specified fallback line
    /// ending.
    /// </summary>
    /// <param name="gitLineEndingResolver">The resolver that reads the git checkout configuration.</param>
    /// <param name="editorConfigLineEndingResolver">The resolver that reads the <c>.editorconfig</c> files.</param>
    /// <param name="operatingSystemLineEnding">
    /// One of the enumeration values that specifies the line ending that is used if neither git nor the
    /// <c>.editorconfig</c> files decide.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="gitLineEndingResolver" /> or <paramref name="editorConfigLineEndingResolver" /> is
    /// <see langword="null" />.
    /// </exception>
    internal LineEndingResolver(IGitLineEndingResolver gitLineEndingResolver,
        IEditorConfigLineEndingResolver editorConfigLineEndingResolver, LineEnding operatingSystemLineEnding)
    {
        _gitLineEndingResolver = Ensure.NotNull(gitLineEndingResolver);
        _editorConfigLineEndingResolver = Ensure.NotNull(editorConfigLineEndingResolver);
        _operatingSystemLineEnding = operatingSystemLineEnding;
    }

    /// <inheritdoc />
    public async Task<LineEndingResolution> GetLineEndingAsync(string repositoryRootPath, string relativePath,
        CancellationToken cancellationToken = default)
    {
        // Validates both arguments before any source is asked.
        var fullPath = RepositoryFilePath.GetFullPath(repositoryRootPath, relativePath);

        var gitLineEnding = await _gitLineEndingResolver
            .GetLineEndingAsync(repositoryRootPath, relativePath, cancellationToken)
            .ConfigureAwait(false);

        if (gitLineEnding is not null)
        {
            return new LineEndingResolution(gitLineEnding.Value, []);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var editorConfigResult = _editorConfigLineEndingResolver.GetLineEnding(fullPath);

        return new LineEndingResolution(editorConfigResult.LineEnding ?? _operatingSystemLineEnding,
            editorConfigResult.Warning is null ? [] : [editorConfigResult.Warning]);
    }
}
