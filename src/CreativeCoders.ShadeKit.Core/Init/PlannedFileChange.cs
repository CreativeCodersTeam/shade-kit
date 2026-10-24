using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Represents the change of a repository file that the initialisation writes, computed before anything is written.
/// </summary>
/// <param name="Content">
/// The complete content the file is written with; its line ending is decided when it is written.
/// </param>
/// <param name="Action">One of the enumeration values that specifies how the write changes the file.</param>
public sealed record PlannedFileChange(TextFileContent Content, InitializedFileAction Action);
