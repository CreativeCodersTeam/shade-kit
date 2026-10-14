namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Provides the definitions of all versioned JSON file types the engine supports.
/// </summary>
public interface IFormatRegistry
{
    /// <summary>
    /// Gets the definitions of all registered file types.
    /// </summary>
    /// <value>The registered file type definitions in registration order.</value>
    IReadOnlyList<FormatDefinition> Formats { get; }

    /// <summary>
    /// Gets the definition of the specified file type.
    /// </summary>
    /// <param name="formatId">The id of the file type, for example <see cref="FormatIds.Config" />.</param>
    /// <returns>The definition of the file type with the id <paramref name="formatId" />.</returns>
    /// <exception cref="ArgumentException">No file type with the id <paramref name="formatId" /> is registered.</exception>
    FormatDefinition GetFormat(string formatId);
}
