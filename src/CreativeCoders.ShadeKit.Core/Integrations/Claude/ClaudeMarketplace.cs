namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Represents a plugin marketplace that Claude Code knows, as <c>claude plugin marketplace list --json</c> reports it.
/// </summary>
/// <param name="Name">The name of the marketplace, for example <c>creativecoders-shade-kit</c>.</param>
/// <param name="SourceKind">
/// The kind of the source of the marketplace, for example <see cref="GitHubSourceKind" /> or
/// <see cref="DirectorySourceKind" />, or <see langword="null" /> if <c>claude</c> reported none.
/// </param>
/// <param name="Repo">
/// The GitHub repository of a <see cref="GitHubSourceKind" /> source in the form <c>owner/repo</c>, or
/// <see langword="null" /> if <c>claude</c> reported none.
/// </param>
/// <param name="Ref">
/// The branch or tag of a <see cref="GitHubSourceKind" /> source, or <see langword="null" /> if <c>claude</c>
/// reported none, which means the default branch of the repository.
/// </param>
/// <param name="Path">
/// The absolute path of a <see cref="DirectorySourceKind" /> source, or <see langword="null" /> if <c>claude</c>
/// reported none.
/// </param>
public sealed record ClaudeMarketplace(string Name, string? SourceKind, string? Repo, string? Ref, string? Path)
{
    /// <summary>
    /// Represents the source kind of a marketplace in a GitHub repository.
    /// </summary>
    public const string GitHubSourceKind = "github";

    /// <summary>
    /// Represents the source kind of a marketplace in a local directory.
    /// </summary>
    public const string DirectorySourceKind = "directory";
}
