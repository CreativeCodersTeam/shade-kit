using System.Diagnostics.CodeAnalysis;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Complete;

/// <summary>
/// Represents the options of the <see cref="PhaseCompleteCommand" />.
/// </summary>
[SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty",
    Justification = "Every command needs its own options type; this command has no options beyond --json.")]
public class PhaseCompleteCommandOptions : JsonCommandOptions;
