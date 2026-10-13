using CreativeCoders.Cli.Hosting;
using CreativeCoders.Cli.Hosting.Help;
using CreativeCoders.ShadeKit.Core;

namespace CreativeCoders.ShadeKit.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var exitCode = await CliHostBuilder.Create()
            .ConfigureServices(x => x.AddShadeKitCore())
            .UseValidation()
            .PrintFooterText([""])
            .EnableHelp(HelpCommandKind.CommandOrArgument, HelpCommandKind.EmptyArgs)
            .Build()
            .RunMainAsync(args)
            .ConfigureAwait(false);

        return ShadeKitCliExitCodes.ToProcessExitCode(exitCode);
    }
}
