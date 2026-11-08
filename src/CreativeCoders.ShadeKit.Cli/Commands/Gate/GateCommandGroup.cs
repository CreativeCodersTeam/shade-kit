using CreativeCoders.Cli.Core;
using CreativeCoders.ShadeKit.Cli.Commands.Gate;

[assembly: CliCommandGroup([GateCommandGroup.Name], "Commands for reading the gates of a feature's workflow")]

namespace CreativeCoders.ShadeKit.Cli.Commands.Gate;

/// <summary>
/// Defines the command group that contains the commands for reading the gates between the phases of a feature's
/// workflow profile.
/// </summary>
public static class GateCommandGroup
{
    /// <summary>
    /// Represents the group name used on the command line.
    /// </summary>
    public const string Name = "gate";
}
