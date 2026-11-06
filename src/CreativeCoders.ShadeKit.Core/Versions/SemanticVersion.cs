using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Versions;

/// <summary>
/// Represents a version that follows Semantic Versioning 2.0.0.
/// </summary>
/// <remarks>
/// The parser accepts exactly the grammar of <see href="https://semver.org/spec/v2.0.0.html">SemVer 2.0.0</see>:
/// <c>major.minor.patch</c> without leading zeros, an optional <c>-prerelease</c> part, and an optional
/// <c>+build</c> part. Surrounding whitespace, a <c>v</c> prefix, and numeric parts greater than
/// <see cref="int.MaxValue" /> are rejected. The build metadata is accepted but not kept, so it takes no part in
/// equality and <see cref="ToString" />.
/// </remarks>
public sealed partial record SemanticVersion
{
    private SemanticVersion(int major, int minor, int patch, string? prerelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
    }

    /// <summary>
    /// Gets the major version.
    /// </summary>
    /// <value>The major version, zero or greater.</value>
    public int Major { get; }

    /// <summary>
    /// Gets the minor version.
    /// </summary>
    /// <value>The minor version, zero or greater.</value>
    public int Minor { get; }

    /// <summary>
    /// Gets the patch version.
    /// </summary>
    /// <value>The patch version, zero or greater.</value>
    public int Patch { get; }

    /// <summary>
    /// Gets the prerelease label.
    /// </summary>
    /// <value>
    /// The dot-separated prerelease identifiers without the leading hyphen, for example <c>beta.1</c>, or
    /// <see langword="null" /> if the version has no prerelease label.
    /// </value>
    public string? Prerelease { get; }

    /// <summary>
    /// Gets a value that indicates whether the version is a prerelease.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if the version has a prerelease label; otherwise, <see langword="false" />.
    /// </value>
    public bool IsPrerelease => Prerelease is not null;

    /// <summary>
    /// Converts the string representation of a SemVer 2.0.0 version to its <see cref="SemanticVersion" />
    /// equivalent. A return value indicates whether the conversion succeeded.
    /// </summary>
    /// <param name="value">A string that contains the version to convert.</param>
    /// <param name="version">
    /// When this method returns, contains the parsed version if the conversion succeeded, or
    /// <see langword="null" /> if it failed. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="value" /> was converted successfully; otherwise,
    /// <see langword="false" />, also if <paramref name="value" /> is <see langword="null" />.
    /// </returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out SemanticVersion? version)
    {
        version = null;

        if (value is null)
        {
            return false;
        }

        var match = SemanticVersionRegex().Match(value);

        if (!match.Success
            || !TryParseNumber(match.Groups["major"], out var major)
            || !TryParseNumber(match.Groups["minor"], out var minor)
            || !TryParseNumber(match.Groups["patch"], out var patch))
        {
            return false;
        }

        var prerelease = match.Groups["prerelease"];

        version = new SemanticVersion(major, minor, patch, prerelease.Success ? prerelease.Value : null);

        return true;
    }

    /// <summary>
    /// Converts the string representation of a SemVer 2.0.0 version to its <see cref="SemanticVersion" />
    /// equivalent.
    /// </summary>
    /// <param name="value">A string that contains the version to convert.</param>
    /// <returns>The version that <paramref name="value" /> represents.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value" /> is <see langword="null" />.</exception>
    /// <exception cref="FormatException"><paramref name="value" /> is not a valid SemVer 2.0.0 version.</exception>
    public static SemanticVersion Parse(string value)
    {
        Ensure.NotNull(value);

        return TryParse(value, out var version)
            ? version
            : throw new FormatException($"'{value}' is not a valid SemVer 2.0.0 version.");
    }

    /// <summary>
    /// Returns the string representation of the version.
    /// </summary>
    /// <returns>
    /// The version in the form <c>major.minor.patch</c>, followed by <c>-prerelease</c> if the version has a
    /// prerelease label; the build metadata of the parsed text is not included.
    /// </returns>
    public override string ToString()
    {
        var version = string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

        return Prerelease is null
            ? version
            : $"{version}-{Prerelease}";
    }

    private static bool TryParseNumber(Group group, out int number)
    {
        return int.TryParse(group.ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    // The grammar of semver.org with [0-9] instead of \d (which also matches non-ASCII digits) and \z instead of $
    // (which also matches before a trailing line break).
    [GeneratedRegex(
        @"^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)"
        + @"(?:-(?<prerelease>(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*)"
        + @"(?:\.(?:0|[1-9][0-9]*|[0-9]*[a-zA-Z-][0-9a-zA-Z-]*))*))?"
        + @"(?:\+[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*)?\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionRegex();
}
