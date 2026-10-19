using System.Diagnostics.CodeAnalysis;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.List;

/// <summary>
/// Represents the options of the <see cref="FeatureListCommand" />.
/// </summary>
[SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty",
    Justification = "Every command needs its own options type; this command has no options beyond --json.")]
public class FeatureListCommandOptions : JsonCommandOptions;
