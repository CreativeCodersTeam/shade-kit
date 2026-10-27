using CreativeCoders.Cli.Core;
using CreativeCoders.ShadeKit.Cli.Commands.Phase;

[assembly: CliCommandGroup([PhaseCommandGroup.Name], "Commands for moving the current feature through its workflow phases")]

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase;

/// <summary>
/// Defines the command group that contains the commands for moving the current feature through the phases of its
/// workflow profile.
/// </summary>
public static class PhaseCommandGroup
{
    /// <summary>
    /// Represents the group name used on the command line.
    /// </summary>
    public const string Name = "phase";
}
