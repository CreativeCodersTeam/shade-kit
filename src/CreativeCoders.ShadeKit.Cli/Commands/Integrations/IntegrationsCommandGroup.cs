using CreativeCoders.Cli.Core;
using CreativeCoders.ShadeKit.Cli.Commands.Integrations;

[assembly: CliCommandGroup([IntegrationsCommandGroup.Name],
    "Commands for installing and updating the SHADE kit integrations of the agents")]

namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations;

/// <summary>
/// Defines the command group that contains the commands for installing and updating the SHADE kit integrations of the
/// supported agents, such as the plugin of Claude Code.
/// </summary>
public static class IntegrationsCommandGroup
{
    /// <summary>
    /// Represents the group name used on the command line.
    /// </summary>
    public const string Name = "integrations";
}
