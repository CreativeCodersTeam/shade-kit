namespace CreativeCoders.ShadeKit.Tests.Shared;

/// <summary>
/// Provides helpers for tests that check the bytes and lines of text files.
/// </summary>
internal static class TestText
{
    /// <summary>
    /// Gets the UTF-8 byte order mark.
    /// </summary>
    public static byte[] Utf8Bom { get; } = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Counts the lines of a text, separated by <c>\n</c>, whose trimmed text starts with a prefix.
    /// </summary>
    public static int CountLinesStartingWith(string text, string prefix)
    {
        return text.Split('\n').Count(x => x.Trim().StartsWith(prefix, StringComparison.Ordinal));
    }
}
