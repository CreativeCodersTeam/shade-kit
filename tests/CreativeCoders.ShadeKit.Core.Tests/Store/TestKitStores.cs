using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Files;

namespace CreativeCoders.ShadeKit.Core.Tests.Store;

/// <summary>
/// Creates real <see cref="KitStore" /> instances for tests.
/// </summary>
internal static class TestKitStores
{
    /// <summary>
    /// Creates a store for the specified <c>.shadekit</c> directory with a serializer for the specified registry.
    /// </summary>
    /// <param name="kitStorePath">The path of the <c>.shadekit</c> directory.</param>
    /// <param name="textFileWriter">
    /// The writer of the store, or <see langword="null" /> for a writer that writes every file with LF.
    /// </param>
    /// <param name="formatRegistry">
    /// The registry of the store and its serializer, or <see langword="null" /> for the default registry.
    /// </param>
    public static KitStore Create(string kitStorePath, ITextFileWriter? textFileWriter = null,
        IFormatRegistry? formatRegistry = null)
    {
        formatRegistry ??= FormatRegistry.CreateDefault();

        return new KitStore(kitStorePath, new VersionedJsonSerializer(formatRegistry), formatRegistry,
            textFileWriter ?? FixedLineEnding.CreateWriter(LineEnding.Lf));
    }
}
