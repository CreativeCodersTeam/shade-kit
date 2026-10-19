namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Represents the change of a repository file that the initialisation writes, computed before anything is written.
/// </summary>
/// <param name="Content">The complete bytes the file is written with.</param>
/// <param name="Action">One of the enumeration values that specifies how the write changes the file.</param>
public sealed record PlannedFileChange(byte[] Content, InitializedFileAction Action);
