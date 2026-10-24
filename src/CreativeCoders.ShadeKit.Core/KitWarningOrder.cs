using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core;

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
    /// <see cref="Store.IKitStore.ReadJsonAsync{T}" /> adds the warnings of an outdated <c>config.json</c> to the
    /// result of every read, and every write determines the line ending again, so a result that reads or writes several
    /// files would otherwise report the same warning once per file. Records are compared by value, so an equal warning
    /// is reported once, at the place of its first occurrence.
    /// </remarks>
    /// <param name="warnings">The warnings in the order in which they came up.</param>
    /// <returns>The warnings in the order in which they are reported.</returns>
    public static IReadOnlyList<KitWarning> Arrange(IEnumerable<KitWarning> warnings)
    {
        KitWarning[] all = [.. warnings];

        return [.. all.Where(x => x is FormatWarning).Concat(all.Where(x => x is not FormatWarning)).Distinct()];
    }
}
