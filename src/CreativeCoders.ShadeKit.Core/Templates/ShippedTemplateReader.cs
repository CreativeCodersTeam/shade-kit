using System.Text;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Templates;

/// <summary>
/// Reads shipped templates from the resources embedded in the SHADE kit core assembly.
/// </summary>
/// <remarks>
/// A template source is embedded under the name <c>templates/</c> followed by its
/// <see cref="ShippedTemplate.ResourcePath" />. The name is the same on every operating system, and embedded
/// resources are also available in a self-contained single-file build.
/// </remarks>
public sealed class ShippedTemplateReader : IShippedTemplateReader
{
    internal const string ResourceNamePrefix = "templates/";

    private readonly Func<string, Stream?> _openResource;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShippedTemplateReader" /> class that reads the resources of the
    /// SHADE kit core assembly.
    /// </summary>
    public ShippedTemplateReader()
        : this(typeof(ShippedTemplateReader).Assembly.GetManifestResourceStream)
    {
    }

    internal ShippedTemplateReader(Func<string, Stream?> openResource)
    {
        _openResource = Ensure.NotNull(openResource);
    }

    /// <inheritdoc />
    public string ReadContent(ShippedTemplate shippedTemplate)
    {
        Ensure.NotNull(shippedTemplate);

        var resourceName = GetResourceName(shippedTemplate);

        using var stream = _openResource(resourceName)
                           ?? throw new InvalidOperationException(
                               $"The shipped template resource '{resourceName}' is missing from the SHADE kit engine.");

        using var reader = new StreamReader(stream, Encoding.UTF8);

        return NormalizeLineEndings(reader.ReadToEnd());
    }

    internal static string GetResourceName(ShippedTemplate shippedTemplate)
    {
        return ResourceNamePrefix + shippedTemplate.ResourcePath;
    }

    // A lone CR is replaced as well: replacing CRLF alone would turn "\r\r\n" into a new CRLF.
    private static string NormalizeLineEndings(string content)
    {
        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}
