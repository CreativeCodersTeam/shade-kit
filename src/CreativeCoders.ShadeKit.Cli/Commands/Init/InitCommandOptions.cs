using System.Diagnostics.CodeAnalysis;

namespace CreativeCoders.ShadeKit.Cli.Commands.Init;

/// <summary>
/// Represents the options of the <see cref="InitCommand" />.
/// </summary>
[SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty",
    Justification = "Every command needs its own options type; this command has no options beyond --json.")]
public class InitCommandOptions : JsonCommandOptions;
