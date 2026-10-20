using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Represents the gate configuration stored in <c>.shadekit/gates.json</c>.
/// </summary>
/// <remarks>
/// The model has no fields of its own yet; package 05 adds the gate definitions. Until then, every field other than
/// <see cref="VersionedDocument.SchemaVersion" /> and <see cref="VersionedDocument.MinEngineVersion" /> is kept in
/// <see cref="VersionedDocument.ExtensionData" /> and written back unchanged.
/// </remarks>
public sealed class GatesConfig : VersionedDocument;
