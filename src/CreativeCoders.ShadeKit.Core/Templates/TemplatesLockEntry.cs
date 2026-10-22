using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CreativeCoders.ShadeKit.Core.Templates;

/// <summary>
/// Represents one installed template in <c>.shadekit/templates.lock.json</c>.
/// </summary>
/// <remarks>
/// <see cref="Path" />, <see cref="KitVersion" />, and <see cref="Hash" /> are required: a lock file in which one of
/// them is missing or <see langword="null" /> cannot be read. Fields the engine does not know are kept in
/// <see cref="ExtensionData" /> and written back unchanged.
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
    /// The SHA-256 hash of the bytes written with CRLF line endings counted as LF, in the form <c>sha256:</c> followed
    /// by 64 lowercase hexadecimal digits.
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
    /// Every CRLF line ending counts as LF, because Git checks a template out with CRLF on Windows when
    /// <c>core.autocrlf</c> is set; an unchanged template then still has the hash of its lock entry. A carriage return
    /// that is not followed by a line feed is hashed as it is.
    /// </remarks>
    /// <param name="content">The bytes of the template as written to the repository.</param>
    /// <returns>
    /// The SHA-256 hash of <paramref name="content" /> with LF line endings, in the form <c>sha256:</c> followed by 64
    /// lowercase hexadecimal digits.
    /// </returns>
    public static string ComputeHash(ReadOnlySpan<byte> content)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var remaining = content;
        var crLfIndex = remaining.IndexOf("\r\n"u8);

        while (crLfIndex >= 0)
        {
            // The carriage return is skipped; the line feed starts the next part.
            hash.AppendData(remaining[..crLfIndex]);
            remaining = remaining[(crLfIndex + 1)..];
            crLfIndex = remaining.IndexOf("\r\n"u8);
        }

        hash.AppendData(remaining);

        return HashPrefix + Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
