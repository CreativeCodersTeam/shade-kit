namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Set;

/// <summary>
/// Represents the <c>data</c> object that <c>shade-kit feature set &lt;id&gt; --json</c> returns.
/// </summary>
/// <param name="Id">The id of the feature that is now selected by the local override.</param>
public sealed record FeatureSetCommandData(string Id);
