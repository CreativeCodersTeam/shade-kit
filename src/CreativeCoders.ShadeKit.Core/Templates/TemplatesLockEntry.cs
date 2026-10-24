using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Templates;

/// <summary>
/// Represents one installed template in <c>.shadekit/templates.lock.json</c>.
/// </summary>
/// <remarks>
/// <see cref="Path" />, <see cref="KitVersion" />, and <see cref="Hash" /> are required. Unknown fields are kept in
/// <see cref="ExtensionData" />.
/// </remarks>
public sealed class TemplatesLockEntry
{
    private const string HashPrefix = "sha256:";

    /// <summary>
    /// Gets the path of the installed template.
    /// </summary>
    /// <value>
    /// The path relative to the repository root, with <c>/</c> as separator, for example
    /// <c>.shadekit/templates/spec.md</c>.
    /// </value>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the kit version that installed the template.
    /// </summary>
    /// <value>A semantic version string.</value>
    public required string KitVersion { get; init; }

    /// <summary>
    /// Gets the hash of the template content as installed.
    /// </summary>
    /// <value>
    /// The SHA-256 hash of the content with its lines joined by LF, whatever line ending the file was written with, in
    /// the form <c>sha256:</c> followed by 64 lowercase hexadecimal digits.
    /// </value>
    public required string Hash { get; init; }

    /// <summary>
    /// Gets or sets the fields of the entry that the engine does not know.
    /// </summary>
    /// <value>
    /// The unknown fields and their values, kept as read, or <see langword="null" /> if the entry has none.
    /// </value>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>
    /// Computes the <see cref="Hash" /> of the content of a template.
    /// </summary>
    /// <remarks>
    /// Hashes the UTF-8 bytes of the lines joined by LF, with the byte order mark and final line ending as recorded, so
    /// the line ending of the written file does not change the hash and older lock entries stay valid.
    /// </remarks>
    /// <param name="content">The content of the template.</param>
    /// <returns>
    /// The SHA-256 hash of <paramref name="content" /> with LF line endings, in the form <c>sha256:</c> followed by 64
    /// lowercase hexadecimal digits.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="content" /> is <see langword="null" />.</exception>
    public static string ComputeHash(TextFileContent content)
    {
        Ensure.NotNull(content);

        return HashPrefix + Convert.ToHexStringLower(SHA256.HashData(TextFileWriter.Encode(content, LineEnding.Lf)));
    }
}
