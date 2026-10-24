using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Features.Warnings;

/// <summary>
/// Represents a warning that the local override cannot be used because of its own format; the override is ignored
/// and the file is left unchanged.
/// </summary>
/// <param name="Problem">
/// The format problem of the local override: an <see cref="InvalidKitFileException" />, a
/// <see cref="FormatTooNewException" />, or an <see cref="UpgradeNeededException" /> for an older format version while
/// <c>outdatedFormatOnRead</c> is <c>stop</c>.
/// </param>
public sealed record CurrentFeatureInvalidWarning(KitFileFormatException Problem) : KitWarning;
