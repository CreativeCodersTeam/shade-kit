using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Warnings;

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
    public async Task<LineEndingResolution> GetLineEndingAsync(RepositoryFile file,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(file);

        var gitResult = await _gitLineEndingResolver.GetLineEndingAsync(file, cancellationToken)
            .ConfigureAwait(false);

        if (gitResult.LineEnding is { } gitLineEnding)
        {
            return new LineEndingResolution(gitLineEnding, []);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var editorConfigResult = _editorConfigLineEndingResolver.GetLineEnding(file.FullPath);

        KitWarning?[] warnings = [gitResult.Warning, editorConfigResult.Warning];

        return new LineEndingResolution(editorConfigResult.LineEnding ?? _operatingSystemLineEnding,
            [.. warnings.OfType<KitWarning>()]);
    }

    /// <inheritdoc />
    public Task PrepareAsync(IReadOnlyCollection<RepositoryFile> files, CancellationToken cancellationToken = default)
    {
        return _gitLineEndingResolver.PreloadAttributesAsync(files, cancellationToken);
    }
}
