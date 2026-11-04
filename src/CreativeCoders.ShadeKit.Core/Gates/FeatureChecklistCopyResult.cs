using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Represents the outcome of making sure that a feature folder contains the filled copy of a checklist.
/// </summary>
/// <param name="Created">
/// <see langword="true" /> if the copy was created from the template; otherwise, <see langword="false" />, because the
/// copy already existed and was left unchanged.
/// </param>
/// <param name="Warnings">
/// The warnings of writing the copy, for example about the line ending; an empty list if the copy was not created or
/// there were none.
/// </param>
public sealed record FeatureChecklistCopyResult(bool Created, IReadOnlyList<KitWarning> Warnings);
