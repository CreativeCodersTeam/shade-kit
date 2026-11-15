using System.Diagnostics.CodeAnalysis;

namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations.Update;

/// <summary>
/// Represents the options of the <see cref="IntegrationsUpdateCommand" />.
/// </summary>
[SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty",
    Justification = "Every command needs its own options type; this command has no options beyond --json.")]
public class IntegrationsUpdateCommandOptions : JsonCommandOptions;
