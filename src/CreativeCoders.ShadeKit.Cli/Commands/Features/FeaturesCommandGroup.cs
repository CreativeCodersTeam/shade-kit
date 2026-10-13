using CreativeCoders.Cli.Core;
using CreativeCoders.ShadeKit.Cli.Commands.Features;

[assembly: CliCommandGroup([FeaturesCommandGroup.Name], "Commands for working with SHADE kit features")]

namespace CreativeCoders.ShadeKit.Cli.Commands.Features;

/// <summary>
/// Defines the command group that contains the commands for working with SHADE kit features.
/// </summary>
public static class FeaturesCommandGroup
{
    /// <summary>
    /// Represents the group name used on the command line.
    /// </summary>
    public const string Name = "features";
}
