using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Provides the definitions of a fixed set of versioned JSON file types.
/// </summary>
public sealed class FormatRegistry : IFormatRegistry
{
    private const string FirstEngineVersion = "0.1.0";

    private readonly Dictionary<string, FormatDefinition> _formatsById;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatRegistry" /> class with the specified file type definitions.
    /// </summary>
    /// <param name="formats">The definitions of the file types to register.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="formats" /> is <see langword="null" /> or contains <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="formats" /> contains more than one definition with the same id.</exception>
    public FormatRegistry(IEnumerable<FormatDefinition> formats)
    {
        Ensure.NotNull(formats);

        Formats = [.. formats.Select(format => Ensure.NotNull(format))];

        var duplicateId = Formats
            .GroupBy(format => format.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Skip(1).Any())?.Key;

        if (duplicateId is not null)
        {
            throw new ArgumentException($"File type '{duplicateId}' is registered more than once.",
                nameof(formats));
        }

        _formatsById = Formats.ToDictionary(format => format.Id, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyList<FormatDefinition> Formats { get; }

    /// <inheritdoc />
    public FormatDefinition GetFormat(string formatId)
    {
        Ensure.NotNull(formatId);

        return _formatsById.TryGetValue(formatId, out var format)
            ? format
            : throw new ArgumentException($"No file type with the id '{formatId}' is registered.", nameof(formatId));
    }

    /// <summary>
    /// Creates a registry with the file types this engine version supports.
    /// </summary>
    /// <remarks>
    /// Every file type listed in <see cref="FormatIds" /> is registered at format version <c>1</c>, which is
    /// supported since engine version <c>0.1.0</c>. Raising a format version means adding an entry to its table and a
    /// migration step here.
    /// </remarks>
    /// <returns>A registry with the definitions of all file types in <see cref="FormatIds" />.</returns>
    public static FormatRegistry CreateDefault()
    {
        return new FormatRegistry(
        [
            CreateVersion1(FormatIds.Config),
            CreateVersion1(FormatIds.Gates),
            CreateVersion1(FormatIds.Feature),
            CreateVersion1(FormatIds.CurrentFeature),
            CreateVersion1(FormatIds.TemplatesLock),
            CreateVersion1(FormatIds.Integrations)
        ]);
    }

    private static FormatDefinition CreateVersion1(string formatId)
    {
        return new FormatDefinition(formatId, new Dictionary<int, string> { [1] = FirstEngineVersion });
    }
}
