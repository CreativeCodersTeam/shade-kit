using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Represents the gate configuration of a SHADE kit store as the engine uses it, together with the warnings of
/// reading it.
/// </summary>
/// <param name="Config">
/// The gate configuration; its <see cref="GatesConfig.Profiles" /> are never <see langword="null" />, because the
/// built-in defaults replace a missing file or a missing <c>profiles</c> field. Ignored entries are kept.
/// </param>
/// <param name="Warnings">
/// The warnings of reading the configuration: the format warnings first, then the gate warnings in file order; an
/// empty list if there were none.
/// </param>
public sealed record GatesConfigReadResult(GatesConfig Config, IReadOnlyList<KitWarning> Warnings);
