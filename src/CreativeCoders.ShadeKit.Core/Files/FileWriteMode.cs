namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Specifies what a write does when the file to write already exists.
/// </summary>
public enum FileWriteMode
{
    /// <summary>
    /// The existing file is overwritten.
    /// </summary>
    Overwrite,

    /// <summary>
    /// The write fails with a <see cref="Exceptions.FileAlreadyExistsException" /> and leaves the existing file
    /// unchanged; the check and the creation of the file are one operation of the file system.
    /// </summary>
    CreateNew
}
