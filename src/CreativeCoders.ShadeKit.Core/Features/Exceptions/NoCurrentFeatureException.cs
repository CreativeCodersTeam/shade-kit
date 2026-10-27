namespace CreativeCoders.ShadeKit.Core.Features.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a command works on the current feature, but neither the current git
/// branch nor the local override selects an existing feature.
/// </summary>
public class NoCurrentFeatureException()
    : Exception("No current feature is selected: neither the current branch nor the local override selects an " +
                "existing feature.");
