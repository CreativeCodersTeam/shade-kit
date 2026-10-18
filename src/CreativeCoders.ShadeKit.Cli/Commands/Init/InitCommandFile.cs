namespace CreativeCoders.ShadeKit.Cli.Commands.Init;

/// <summary>
/// Represents a file or directory in the <c>files</c> list that <c>shade-kit init --json</c> returns.
/// </summary>
/// <param name="Path">
/// The path relative to the repository root, with <c>/</c> as separator, for example <c>.shadekit/config.json</c>. A
/// directory ends with <c>/</c>, for example <c>.shadekit/features/</c>.
/// </param>
/// <param name="Action">
/// How the file was changed: <c>created</c>, <c>appended</c>, or <c>updated</c>.
/// </param>
public sealed record InitCommandFile(string Path, string Action);
