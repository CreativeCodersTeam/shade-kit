namespace CreativeCoders.ShadeKit.Core.Text;

/// <summary>
/// Provides helpers for the sentences of exception reasons and messages.
/// </summary>
internal static class Sentences
{
    /// <summary>
    /// Returns the specified text as a sentence that ends with a period.
    /// </summary>
    /// <param name="text">The text, for example the message of an exception or the standard error of a process.</param>
    /// <returns>
    /// <paramref name="text" /> without leading and trailing whitespace, followed by a period unless it already ends
    /// with one.
    /// </returns>
    public static string Terminate(string text)
    {
        var trimmedText = text.Trim();

        return trimmedText.EndsWith('.') ? trimmedText : trimmedText + ".";
    }
}
