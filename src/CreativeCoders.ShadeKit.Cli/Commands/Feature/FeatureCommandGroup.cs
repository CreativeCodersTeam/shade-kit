using CreativeCoders.Cli.Core;
using CreativeCoders.ShadeKit.Cli.Commands.Feature;

[assembly: CliCommandGroup([FeatureCommandGroup.Name], "Commands for working with SHADE kit features")]

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature;

/// <summary>
/// Defines the command group that contains the commands for working with SHADE kit features.
/// </summary>
public static class FeatureCommandGroup
{
    /// <summary>
    /// Represents the group name used on the command line.
    /// </summary>
    public const string Name = "feature";
}
