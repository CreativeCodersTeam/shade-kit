using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Gates.Warnings;

/// <summary>
/// Represents a warning that <c>.shadekit/gates.json</c> configures no gates, so the built-in gate defaults of
/// <see cref="GatesConfig.CreateDefault" /> apply.
/// </summary>
/// <param name="IsFileMissing">
/// <see langword="true" /> if <c>gates.json</c> does not exist; <see langword="false" /> if it exists but has no
/// <c>profiles</c> field.
/// </param>
public sealed record GatesDefaultsUsedWarning(bool IsFileMissing) : KitWarning;
