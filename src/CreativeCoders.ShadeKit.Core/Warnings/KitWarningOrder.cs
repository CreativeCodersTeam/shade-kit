using CreativeCoders.ShadeKit.Core.Formats.Warnings;

namespace CreativeCoders.ShadeKit.Core.Warnings;

/// <summary>
/// Puts the warnings of a result into the order in which they are reported.
/// </summary>
internal static class KitWarningOrder
{
    /// <summary>
    /// Returns the specified warnings with the format warnings first and the other warnings after them, each group in
    /// its original order, and every warning only once.
    /// </summary>
    /// <remarks>
    /// Reads and writes of several files repeat the same warnings; equal records are reported once, at their first
    /// occurrence.
    /// </remarks>
    /// <param name="warnings">The warnings in the order in which they came up.</param>
    /// <returns>The warnings in the order in which they are reported.</returns>
    public static IReadOnlyList<KitWarning> Arrange(IEnumerable<KitWarning> warnings)
    {
        KitWarning[] all = [.. warnings];

        return [.. all.Where(x => x is FormatWarning).Concat(all.Where(x => x is not FormatWarning)).Distinct()];
    }
}
