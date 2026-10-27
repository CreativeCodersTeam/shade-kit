namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Show;

/// <summary>
/// Represents the <c>by</c> object of a <c>history</c> entry in the <c>data</c> of
/// <c>shade-kit feature show &lt;id&gt; --json</c>.
/// </summary>
/// <param name="Name">The git user name of the person who changed the phase state.</param>
/// <param name="Email">The git user email of the person who changed the phase state.</param>
public sealed record FeatureShowCommandHistoryActorData(string Name, string Email);
