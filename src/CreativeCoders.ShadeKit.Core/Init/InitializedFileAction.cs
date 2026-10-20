namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Specifies how the initialisation of a repository changed a file.
/// </summary>
public enum InitializedFileAction
{
    /// <summary>
    /// The file did not exist and was created. Reported as <c>created</c>.
    /// </summary>
    Created,

    /// <summary>
    /// The file existed and content was added to its end. Reported as <c>appended</c>.
    /// </summary>
    Appended,

    /// <summary>
    /// The file existed and a part of its content was replaced. Reported as <c>updated</c>.
    /// </summary>
    Updated
}
