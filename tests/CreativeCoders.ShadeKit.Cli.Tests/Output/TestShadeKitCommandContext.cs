using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Store;
using FakeItEasy;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

/// <summary>
/// Creates real <see cref="ShadeKitCommandContext" /> instances for the commands under test.
/// </summary>
internal static class TestShadeKitCommandContext
{
    /// <summary>
    /// Creates a context with a real <see cref="CliJsonWriter" /> and a real <see cref="CliErrorMapper" />.
    /// </summary>
    /// <param name="console">The console that receives the plain output.</param>
    /// <param name="kitStoreFactory">
    /// The factory that locates the SHADE kit store, or <see langword="null" /> for a fake factory.
    /// </param>
    /// <param name="jsonOutput">
    /// The writer that receives the JSON output, or <see langword="null" /> for <see cref="TextWriter.Null" />.
    /// </param>
    /// <param name="cancellation">
    /// The cancellation of the run, or <see langword="null" /> for <see cref="TestCliCancellation.None" />.
    /// </param>
    public static ShadeKitCommandContext Create(IAnsiConsole console, IKitStoreFactory? kitStoreFactory = null,
        TextWriter? jsonOutput = null, ICliCancellation? cancellation = null)
    {
        return new ShadeKitCommandContext(console, kitStoreFactory ?? A.Fake<IKitStoreFactory>(),
            new CliJsonWriter(jsonOutput ?? TextWriter.Null), CliErrorMapperTests.CreateMapper(),
            cancellation ?? TestCliCancellation.None);
    }
}
