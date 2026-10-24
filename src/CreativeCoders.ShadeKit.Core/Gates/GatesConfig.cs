using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Represents the gate configuration stored in <c>.shadekit/gates.json</c>.
/// </summary>
/// <remarks>
/// No fields of its own yet; all fields are kept in <see cref="VersionedDocument.ExtensionData" />.
/// </remarks>
public sealed class GatesConfig : VersionedDocument;
