using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Features.Warnings;

/// <summary>
/// Represents a warning that the discussion notes moved into a new feature were copied, but the original file could
/// not be deleted.
/// </summary>
/// <param name="FilePath">
/// The repository-relative path of the original file, with <c>/</c> as separator, for example
/// <c>.shadekit/discussions/20261020-1500-login-sso.md</c>.
/// </param>
/// <param name="Reason">The message of the error that prevented the deletion.</param>
public sealed record FeatureSourceNotRemovedWarning(string FilePath, string Reason) : KitWarning;
