using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;

namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Converts versioned JSON documents between their text and their object form using the file type definitions of an
/// <see cref="IFormatRegistry" />.
/// </summary>
/// <param name="formatRegistry">The registry that provides the file type definitions.</param>
public sealed class VersionedJsonSerializer(IFormatRegistry formatRegistry) : IVersionedJsonSerializer
{
    private const string SchemaVersionField = "schemaVersion";

    private const string MinEngineVersionField = "minEngineVersion";

    private static readonly JsonDocumentOptions DocumentOptions = new JsonDocumentOptions { AllowDuplicateProperties = false };

    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        RespectNullableAnnotations = true,
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new SingleValueJsonStringEnumConverter(JsonEnumNames.NamingPolicy) }
    };

    private readonly IFormatRegistry _formatRegistry = Ensure.NotNull(formatRegistry);

    /// <inheritdoc />
    public VersionedReadResult<T> Deserialize<T>(string json, string formatId, string filePath, bool migrateOutdated)
        where T : VersionedDocument
    {
        Ensure.NotNull(json);
        Ensure.NotNull(filePath);

        var format = _formatRegistry.GetFormat(formatId);
        var root = ParseRoot(json, filePath);
        var fileVersion = ReadSchemaVersion(root, filePath);
        var minEngineVersion = ReadMinEngineVersion(root);

        if (fileVersion > format.CurrentVersion)
        {
            throw new FormatTooNewException(filePath, fileVersion, format.CurrentVersion, minEngineVersion);
        }

        if (minEngineVersion is null)
        {
            throw new InvalidKitFileException(filePath,
                $"The '{MinEngineVersionField}' field is missing or not a non-empty string.");
        }

        if (fileVersion == format.CurrentVersion)
        {
            return new VersionedReadResult<T>(ToDocument<T>(root, filePath), []);
        }

        if (!migrateOutdated)
        {
            throw new UpgradeNeededException(filePath, fileVersion, format.CurrentVersion);
        }

        Migrate(root, format, fileVersion, filePath);

        return new VersionedReadResult<T>(ToDocument<T>(root, filePath),
            [new FormatWarning(FormatWarningKind.UpgradeNeeded, filePath, fileVersion, format.CurrentVersion)]);
    }

    /// <inheritdoc />
    public string Serialize<T>(T document, string formatId)
        where T : VersionedDocument
    {
        Ensure.NotNull(document);

        var format = _formatRegistry.GetFormat(formatId);
        // A non-null object of a class type always serializes to a JSON object, never to null.
        var root = JsonSerializer.SerializeToNode(document, document.GetType(), SerializerOptions)!.AsObject();

        root[SchemaVersionField] = format.CurrentVersion;
        root[MinEngineVersionField] = format.GetMinEngineVersion(format.CurrentVersion);

        return root.ToJsonString(SerializerOptions) + "\n";
    }

    private static JsonObject ParseRoot(string json, string filePath)
    {
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(json, documentOptions: DocumentOptions);
        }
        catch (JsonException e)
        {
            throw new InvalidKitFileException(filePath, $"The content is not valid JSON ({e.Message}).", e);
        }

        return root as JsonObject
               ?? throw new InvalidKitFileException(filePath, "The content is not a JSON object.");
    }

    private static int ReadSchemaVersion(JsonObject root, string filePath)
    {
        if (!root.TryGetPropertyValue(SchemaVersionField, out var node) || node is null)
        {
            throw new InvalidKitFileException(filePath, $"The '{SchemaVersionField}' field is missing.");
        }

        if (node is not JsonValue value
            || value.GetValueKind() != JsonValueKind.Number
            || !value.TryGetValue<int>(out var version))
        {
            throw new InvalidKitFileException(filePath, $"The '{SchemaVersionField}' field is not an integer.");
        }

        return version >= 1
            ? version
            : throw new InvalidKitFileException(filePath, $"The '{SchemaVersionField}' field must be 1 or greater.");
    }

    private static string? ReadMinEngineVersion(JsonObject root)
    {
        return root.TryGetPropertyValue(MinEngineVersionField, out var node)
               && node is JsonValue value
               && value.GetValueKind() == JsonValueKind.String
               && value.GetValue<string>() is { } minEngineVersion
               && !string.IsNullOrWhiteSpace(minEngineVersion)
            ? minEngineVersion
            : null;
    }

    private static void Migrate(JsonObject root, FormatDefinition format, int fileVersion, string filePath)
    {
        foreach (var migration in format.GetMigrations(fileVersion))
        {
            try
            {
                migration.Migrate(root);
            }
            catch (Exception e) when (e is not KitFileFormatException)
            {
                throw new InvalidKitFileException(filePath,
                    $"The migration from format version {migration.FromVersion} failed ({e.Message}).", e);
            }

            root[SchemaVersionField] = migration.FromVersion + 1;
        }

        root[MinEngineVersionField] = format.GetMinEngineVersion(format.CurrentVersion);
    }

    private static T ToDocument<T>(JsonObject root, string filePath)
        where T : VersionedDocument
    {
        try
        {
            return root.Deserialize<T>(SerializerOptions)
                   ?? throw new InvalidKitFileException(filePath, "The content is not a JSON object.");
        }
        catch (JsonException e)
        {
            throw new InvalidKitFileException(filePath, $"A field has an unexpected value ({e.Message}).", e);
        }
    }
}
