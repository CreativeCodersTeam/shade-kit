using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Codex;

/// <summary>
/// Locates the OpenAI Codex CLI executable <c>codex</c> in the absolute directories of the <c>PATH</c> environment
/// variable.
/// </summary>
/// <remarks>
/// <para>
/// The executable is <c>codex</c> with the Unix rules. With the Windows rules it is <c>codex.exe</c> or
/// <c>codex.cmd</c>: each absolute <c>PATH</c> directory is checked for <c>codex.exe</c>, then for <c>codex.cmd</c>,
/// before the next directory, as described for <see cref="PathExecutableSearch" />. A <c>codex.cmd</c> is the shim of
/// an npm installation; the <see cref="CodexCommandRunner" /> runs it through <c>cmd.exe</c>. <c>codex.ps1</c> is not
/// supported.
/// </para>
/// <para>
/// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>): the binary is named <c>codex</c>
/// (<c>codex-rs/cli/Cargo.toml</c>), the npm package <c>@openai/codex</c> has the bin <c>codex</c>
/// (<c>codex-cli/package.json</c>), and the Windows install script installs <c>codex.exe</c>
/// (<c>scripts/install/install.ps1</c>). Unverified: that an npm installation on Windows puts <c>codex.cmd</c> on the
/// <c>PATH</c> (standard npm behaviour) and that the install script puts its directory on the <c>PATH</c>.
/// </para>
/// </remarks>
public sealed class CodexExecutableLocator : PathExecutableLocator, ICodexExecutableLocator
{
    private const string UnixExecutableName = "codex";

    private static readonly string[] WindowsExecutableNames = ["codex.exe", "codex.cmd"];

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexExecutableLocator" /> class that reads the <c>PATH</c>
    /// environment variable of the current process and uses the rules of the current operating system.
    /// </summary>
    public CodexExecutableLocator()
        : base(new PathExecutableSearch(WindowsExecutableNames, UnixExecutableName))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexExecutableLocator" /> class with the specified sources for
    /// the <c>PATH</c> value, the operating system rules, the file check, and the execute check, as described for
    /// <see cref="PathExecutableSearch" />.
    /// </summary>
    /// <param name="getPathVariable">
    /// The function that returns the value of the <c>PATH</c> environment variable, or <see langword="null" /> if it
    /// is not set.
    /// </param>
    /// <param name="isWindows">
    /// <see langword="true" /> to use the Windows rules; <see langword="false" /> to use the Unix rules.
    /// </param>
    /// <param name="fileExists">
    /// The function that returns <see langword="true" /> if the specified path is an existing file.
    /// </param>
    /// <param name="isExecutable">
    /// The function that returns <see langword="true" /> if the existing file at the specified path has at least one
    /// execute bit; it is called only with the Unix rules.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="getPathVariable" />, <paramref name="fileExists" />, or <paramref name="isExecutable" /> is
    /// <see langword="null" />.
    /// </exception>
    internal CodexExecutableLocator(Func<string?> getPathVariable, bool isWindows, Func<string, bool> fileExists,
        Func<string, bool> isExecutable)
        : base(new PathExecutableSearch(WindowsExecutableNames, UnixExecutableName, getPathVariable, isWindows,
            fileExists, isExecutable))
    {
    }

    /// <inheritdoc />
    public string? FindCodexExecutable()
    {
        return FindExecutable();
    }
}
