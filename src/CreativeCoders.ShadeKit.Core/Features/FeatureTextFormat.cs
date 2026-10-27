using System.Globalization;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Formats the values of a feature as text in the same way wherever SHADE kit writes them for people, for example in
/// the <c>request.md</c> of a tracker issue and in the plain output of <c>shade-kit feature show</c>.
/// </summary>
public static class FeatureTextFormat
{
    /// <summary>
    /// Represents the custom format string of a creation time: ISO 8601 with whole seconds and the UTC offset.
    /// </summary>
    public const string CreatedAtFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    /// <summary>
    /// Formats the specified creation time of a feature.
    /// </summary>
    /// <param name="createdAt">The creation time with its UTC offset.</param>
    /// <returns>
    /// The creation time in ISO 8601 format with the offset of <paramref name="createdAt" />, for example
    /// <c>2026-10-20T15:30:45+02:00</c>.
    /// </returns>
    public static string FormatCreatedAt(DateTimeOffset createdAt)
    {
        return createdAt.ToString(CreatedAtFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats the specified source of a feature as one line.
    /// </summary>
    /// <remarks>
    /// The text of a text source can span many lines, so it is left out; it is in <c>request.md</c>.
    /// </remarks>
    /// <param name="source">The source of the feature.</param>
    /// <returns>
    /// The kind followed by the URL or the file path, for example <c>url https://github.com/octo/repo/issues/1</c>,
    /// or the kind alone for a text source or a source without value, for example <c>text</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source" /> is <see langword="null" />.</exception>
    public static string FormatSource(FeatureSource source)
    {
        Ensure.NotNull(source);

        return source.Kind == FeatureSourceKind.Text || source.Value is null
            ? source.Kind.ToJsonName()
            : $"{source.Kind.ToJsonName()} {source.Value}";
    }

    /// <summary>
    /// Formats the creation time and the creator of the specified feature as one line.
    /// </summary>
    /// <param name="feature">The <c>feature.json</c> document of the feature.</param>
    /// <returns>
    /// The text <c>&lt;createdAt&gt; by &lt;name&gt; &lt;&lt;email&gt;&gt;</c>, for example
    /// <c>2026-10-20T15:30:45+02:00 by Jane Doe &lt;jane@example.com&gt;</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="feature" /> is <see langword="null" />.</exception>
    public static string FormatCreated(FeatureDocument feature)
    {
        Ensure.NotNull(feature);

        return $"{FormatCreatedAt(feature.CreatedAt)} by {feature.CreatedBy.Name} <{feature.CreatedBy.Email}>";
    }

    /// <summary>
    /// Formats the specified entry of the phase history of a feature as one line.
    /// </summary>
    /// <remarks>
    /// The point in time uses the format of <see cref="FormatCreatedAt" />. The action is written with its JSON name.
    /// A missing <see cref="PhaseHistoryEntry.From" /> or <see cref="PhaseHistoryEntry.To" /> is left out, for
    /// example the target of a <c>complete</c> entry. The note is written unchanged, so a note with line breaks spans
    /// several lines.
    /// </remarks>
    /// <param name="entry">The <c>history</c> entry of the feature.</param>
    /// <returns>
    /// The text <c>&lt;at&gt; &lt;action&gt; &lt;from&gt; -&gt; &lt;to&gt; by &lt;name&gt; &lt;&lt;email&gt;&gt;</c>,
    /// followed by <c>, note: &lt;note&gt;</c> if the entry has a note, for example
    /// <c>2026-10-21T09:00:00+02:00 reject refine -&gt; map by Jane Doe &lt;jane@example.com&gt;, note: DOR-04
    /// failed</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry" /> is <see langword="null" />.</exception>
    public static string FormatHistoryEntry(PhaseHistoryEntry entry)
    {
        Ensure.NotNull(entry);

        var transition = (entry.From, entry.To) switch
        {
            ({ } from, { } to) => $" {from} -> {to}",
            ({ } from, null) => $" {from}",
            (null, { } to) => $" -> {to}",
            _ => string.Empty
        };

        var note = entry.Note is null ? string.Empty : $", note: {entry.Note}";

        return $"{FormatCreatedAt(entry.At)} {entry.Action.ToJsonName()}{transition} by {entry.By.Name} " +
               $"<{entry.By.Email}>{note}";
    }
}
