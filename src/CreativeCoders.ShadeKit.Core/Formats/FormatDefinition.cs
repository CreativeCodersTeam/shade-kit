using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Describes one versioned JSON file type: its id, its current format version, the minimum engine version of each
/// format version, and its migration chain.
/// </summary>
public sealed class FormatDefinition
{
    private readonly Dictionary<int, string> _minEngineVersions;

    private readonly Dictionary<int, IFormatMigration> _migrations;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatDefinition" /> class.
    /// </summary>
    /// <param name="id">The id of the file type, for example <see cref="FormatIds.Config" />.</param>
    /// <param name="minEngineVersions">
    /// A table that maps every format version, starting at <c>1</c> without gaps, to the first engine version that
    /// supports it. The highest version in the table is the current format version.
    /// </param>
    /// <param name="migrations">
    /// The migration steps of the file type, one for each format version below the current one, or
    /// <see langword="null" /> if the file type has only one format version.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="minEngineVersions" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="id" /> is <see langword="null" />, empty, or whitespace; the table is empty, has gaps, does not
    /// start at <c>1</c>, or contains an empty engine version; or the migration steps belong to another file type,
    /// are duplicated, or do not cover every format version below the current one.
    /// </exception>
    public FormatDefinition(string id, IReadOnlyDictionary<int, string> minEngineVersions,
        IEnumerable<IFormatMigration>? migrations = null)
    {
        Id = Ensure.IsNotNullOrWhitespace(id);
        Ensure.NotNull(minEngineVersions);

        CurrentVersion = ValidateMinEngineVersions(minEngineVersions);
        _minEngineVersions = new Dictionary<int, string>(minEngineVersions);
        _migrations = CreateMigrationTable(id, CurrentVersion, migrations ?? []);
    }

    /// <summary>
    /// Gets the id of the file type.
    /// </summary>
    /// <value>The logical id of the file type, for example <see cref="FormatIds.Config" />.</value>
    public string Id { get; }

    /// <summary>
    /// Gets the current format version of the file type.
    /// </summary>
    /// <value>The format version this engine writes.</value>
    public int CurrentVersion { get; }

    /// <summary>
    /// Gets the first engine version that supports the specified format version.
    /// </summary>
    /// <param name="formatVersion">The format version to look up.</param>
    /// <returns>The semantic version string of the first engine that supports <paramref name="formatVersion" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="formatVersion" /> is less than <c>1</c> or greater than <see cref="CurrentVersion" />.
    /// </exception>
    public string GetMinEngineVersion(int formatVersion)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(formatVersion, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(formatVersion, CurrentVersion);

        return _minEngineVersions[formatVersion];
    }

    /// <summary>
    /// Gets the migration steps that convert a document from the specified format version to the current one.
    /// </summary>
    /// <param name="fromVersion">The format version of the document to migrate.</param>
    /// <returns>
    /// The migration steps in the order they must be applied; an empty list if <paramref name="fromVersion" /> is
    /// the current format version.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fromVersion" /> is less than <c>1</c> or greater than <see cref="CurrentVersion" />.
    /// </exception>
    public IReadOnlyList<IFormatMigration> GetMigrations(int fromVersion)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fromVersion, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fromVersion, CurrentVersion);

        return [.. Enumerable.Range(fromVersion, CurrentVersion - fromVersion).Select(version => _migrations[version])];
    }

    private static int ValidateMinEngineVersions(IReadOnlyDictionary<int, string> minEngineVersions)
    {
        if (minEngineVersions.Count == 0)
        {
            throw new ArgumentException("The table of minimum engine versions must not be empty.",
                nameof(minEngineVersions));
        }

        var currentVersion = minEngineVersions.Count;

        for (var version = 1; version <= currentVersion; version++)
        {
            if (!minEngineVersions.TryGetValue(version, out var minEngineVersion))
            {
                throw new ArgumentException(
                    $"The table of minimum engine versions must contain every format version from 1 to " +
                    $"{currentVersion}; version {version} is missing.", nameof(minEngineVersions));
            }

            if (string.IsNullOrWhiteSpace(minEngineVersion))
            {
                throw new ArgumentException(
                    $"The minimum engine version of format version {version} must not be empty.",
                    nameof(minEngineVersions));
            }
        }

        return currentVersion;
    }

    private static Dictionary<int, IFormatMigration> CreateMigrationTable(string id, int currentVersion,
        IEnumerable<IFormatMigration> migrations)
    {
        var migrationTable = new Dictionary<int, IFormatMigration>();

        foreach (var migration in migrations)
        {
            Ensure.NotNull(migration);

            if (migration.FormatId != id)
            {
                throw new ArgumentException(
                    $"The migration from version {migration.FromVersion} belongs to file type " +
                    $"'{migration.FormatId}', not '{id}'.", nameof(migrations));
            }

            if (migration.FromVersion < 1 || migration.FromVersion >= currentVersion)
            {
                throw new ArgumentException(
                    $"The migration from version {migration.FromVersion} is outside the format versions 1 to " +
                    $"{currentVersion} of file type '{id}'.", nameof(migrations));
            }

            if (!migrationTable.TryAdd(migration.FromVersion, migration))
            {
                throw new ArgumentException(
                    $"File type '{id}' has more than one migration from version {migration.FromVersion}.",
                    nameof(migrations));
            }
        }

        if (migrationTable.Count != currentVersion - 1)
        {
            throw new ArgumentException(
                $"File type '{id}' needs one migration for every format version from 1 to {currentVersion - 1}.",
                nameof(migrations));
        }

        return migrationTable;
    }
}
