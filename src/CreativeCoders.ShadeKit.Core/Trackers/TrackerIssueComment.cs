namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Represents a comment of an issue read from a tracker.
/// </summary>
/// <param name="Author">The login of the author of the comment.</param>
/// <param name="CreatedAt">
/// The time at which the comment was created, with the offset that the tracker returned.
/// </param>
/// <param name="Body">The text of the comment, unchanged, or an empty string if the comment has none.</param>
public sealed record TrackerIssueComment(string Author, DateTimeOffset CreatedAt, string Body);
