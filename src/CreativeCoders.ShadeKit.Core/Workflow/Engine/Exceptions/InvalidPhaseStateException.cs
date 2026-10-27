namespace CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the phase fields of a <c>feature.json</c> file do not fit the workflow
/// profile of the feature or contradict each other.
/// </summary>
/// <param name="featureFilePath">The repository-relative path of the <c>feature.json</c> file.</param>
/// <param name="field">The name of the JSON field that is invalid, for example <c>currentPhase</c>.</param>
/// <param name="reason">A sentence that describes why the field is invalid.</param>
public class InvalidPhaseStateException(string featureFilePath, string field, string reason)
    : Exception($"The phase state in '{featureFilePath}' is invalid: the field '{field}' {reason}")
{
    /// <summary>
    /// Gets the path of the <c>feature.json</c> file.
    /// </summary>
    /// <value>The repository-relative path, for example <c>.shadekit/features/gh-142-login-sso/feature.json</c>.</value>
    public string FeatureFilePath { get; } = featureFilePath;

    /// <summary>
    /// Gets the name of the JSON field that is invalid.
    /// </summary>
    /// <value>One of <c>status</c>, <c>currentPhase</c>, <c>phases</c>, or <c>pendingTransition</c>.</value>
    public string Field { get; } = field;

    /// <summary>
    /// Gets the reason why the field is invalid.
    /// </summary>
    /// <value>
    /// A sentence that continues "the field '&lt;field&gt;'", for example <c>is missing, but phases is set.</c>
    /// </value>
    public string Reason { get; } = reason;
}
