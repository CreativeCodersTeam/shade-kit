namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Represents an issue read from a tracker, independent of the tracker and of the tool that read it.
/// </summary>
/// <param name="Title">The title of the issue.</param>
/// <param name="Body">The description of the issue, unchanged, or an empty string if the issue has none.</param>
/// <param name="Comments">The comments of the issue in the order in which the tracker returned them.</param>
public sealed record TrackerIssue(string Title, string Body, IReadOnlyList<TrackerIssueComment> Comments);
