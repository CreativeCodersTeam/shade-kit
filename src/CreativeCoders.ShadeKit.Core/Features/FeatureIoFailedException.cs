using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the exception that is thrown when the folder of a feature cannot be created or renamed, for example
/// because the process may not write to the <c>features</c> folder or another program holds a file in it open.
/// </summary>
/// <remarks>
/// The exception is thrown only by steps that change nothing when they fail: creating the hidden staging folder of a
/// new feature, moving it into place, and renaming the feature folder before it is deleted. Nothing was changed when
/// it is thrown; a staging folder that was already created is removed.
/// </remarks>
/// <param name="featureId">The id of the feature whose folder cannot be created or renamed.</param>
/// <param name="operation">
/// The name of the feature operation that failed, for example <see cref="CreateOperation" /> or
/// <see cref="DropOperation" />.
/// </param>
/// <param name="innerException">
/// The exception of the file system that caused this exception, usually an <see cref="IOException" /> or an
/// <see cref="UnauthorizedAccessException" />.
/// </param>
public class FeatureIoFailedException(string featureId, string operation, Exception innerException)
    : Exception(CreateMessage(featureId, operation, innerException), innerException)
{
    /// <summary>
    /// Represents the name of the operation that creates a new feature, as <c>shade-kit feature new</c> does.
    /// </summary>
    public const string CreateOperation = "create";

    /// <summary>
    /// Represents the name of the operation that drops a feature, as <c>shade-kit feature drop</c> does.
    /// </summary>
    public const string DropOperation = "drop";

    /// <summary>
    /// Gets the id of the feature whose folder cannot be created or renamed.
    /// </summary>
    /// <value>The id of the feature, which is also the name of its folder.</value>
    public string FeatureId { get; } = featureId;

    /// <summary>
    /// Gets the path of the feature folder, relative to the repository root.
    /// </summary>
    /// <value>
    /// The path of the feature folder with a trailing <c>/</c>, for example
    /// <c>.shadekit/features/gh-142-login-sso/</c>.
    /// </value>
    public string FolderPath { get; } = GetFolderPath(featureId);

    /// <summary>
    /// Gets the name of the feature operation that failed.
    /// </summary>
    /// <value>A verb such as <see cref="CreateOperation" /> or <see cref="DropOperation" />.</value>
    public string Operation { get; } = operation;

    private static string CreateMessage(string featureId, string operation, Exception innerException)
    {
        Ensure.NotNull(innerException);

        return $"Could not {operation} the feature '{featureId}', because its folder '{GetFolderPath(featureId)}' " +
               $"cannot be changed: {innerException.Message}";
    }

    private static string GetFolderPath(string featureId)
    {
        return $"{KitStorePaths.GetRepositoryPath(KitStorePaths.GetFeatureDirPath(featureId))}/";
    }
}
