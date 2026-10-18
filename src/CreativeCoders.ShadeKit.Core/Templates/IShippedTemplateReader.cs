namespace CreativeCoders.ShadeKit.Core.Templates;

/// <summary>
/// Reads the content of the templates that ship with SHADE kit.
/// </summary>
public interface IShippedTemplateReader
{
    /// <summary>
    /// Reads the content of a shipped template.
    /// </summary>
    /// <param name="shippedTemplate">The shipped template to read.</param>
    /// <returns>The content of the template, with every CRLF or CR line ending replaced by LF.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="shippedTemplate" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The SHADE kit engine contains no embedded resource for the <see cref="ShippedTemplate.ResourcePath" /> of
    /// <paramref name="shippedTemplate" />.
    /// </exception>
    string ReadContent(ShippedTemplate shippedTemplate);
}
