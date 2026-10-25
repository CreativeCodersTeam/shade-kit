namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the identity of a new feature as derived from its source and title by an
/// <see cref="IFeatureIdFactory" />.
/// </summary>
/// <param name="Id">The valid feature id, for example <c>gh-142-login-sso</c> or <c>20261013-1530-login-sso</c>.</param>
/// <param name="Title">The title of the feature, from which the slug of <paramref name="Id" /> was built.</param>
/// <param name="Source">The kind of the source and the reference to it, as written to <c>feature.json</c>.</param>
/// <param name="RequestContent">
/// The content of <c>request.md</c>: the text as passed for a text source; the file content decoded as UTF-8 without
/// a byte order mark and with its line endings for a file source; the issue formatted as Markdown for a URL whose host
/// is a tracker; and the content of the URL, decoded as UTF-8 without a byte order mark, for any other URL.
/// </param>
/// <param name="CreatedAt">
/// The local creation time, truncated to whole seconds, from which a time-based id was built and which is written as
/// <c>createdAt</c>.
/// </param>
public sealed record FeatureIdentity(
    string Id,
    string Title,
    FeatureSource Source,
    string RequestContent,
    DateTimeOffset CreatedAt);
