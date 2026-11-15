using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Checks the arguments of a Windows <c>.cmd</c> shim and builds the <c>cmd.exe</c> command line that runs it.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the GitHub Copilot CLI and the OpenAI Codex CLI are commonly installed with npm, which puts
/// only a <c>.cmd</c> shim (no <c>.exe</c>) on the <c>PATH</c> on Windows. A <c>.cmd</c> file can only be run by
/// <c>cmd.exe</c>, so running the shim is a documented deviation from the rule that external tools are started without
/// a shell (user decision of 2026-11-15, after an objection about <c>cmd.exe</c> argument injection).
/// </para>
/// <para>
/// <c>cmd.exe</c> does not parse arguments like other Windows programs: it expands <c>%</c> and <c>!</c> even inside
/// double quotes and treats <c>&amp;</c>, <c>|</c>, <c>&lt;</c>, <c>&gt;</c>, <c>^</c>, <c>(</c>, and <c>)</c> as
/// syntax, which allows command injection through arguments (the BatBadBut class, CVE-2024-24576). Escaping for
/// <c>cmd.exe</c> is fragile, so an argument or a shim path that contains one of the refused characters is refused
/// instead of escaped; the arguments that SHADE kit passes never need them.
/// </para>
/// <para>
/// The command line follows the established <c>cmd.exe /d /s /c</c> convention: <c>/d</c> skips the AutoRun commands,
/// <c>/s</c> makes <c>cmd.exe</c> strip exactly the outer pair of double quotes, and the shim path inside is quoted.
/// Unverified: the Windows behaviour has not been run on Windows; only the check and the built command line are
/// unit-tested.
/// </para>
/// </remarks>
internal static class CmdShimCommandLine
{
    /// <summary>
    /// Represents the name of the environment variable that holds the path of the Windows command interpreter.
    /// </summary>
    public const string ComSpecVariableName = "ComSpec";

    /// <summary>
    /// Represents the characters that a shim path or an argument must not contain.
    /// </summary>
    public const string RefusedCharacters = "\"%!^&|<>()\r\n\0";

    private const string ShimExtension = ".cmd";

    private const string CmdExeFileName = "cmd.exe";

    private const char Quote = '"';

    private const char Backslash = '\\';

    private static readonly SearchValues<char> RefusedCharacterValues = SearchValues.Create(RefusedCharacters);

    /// <summary>
    /// Determines whether the specified executable path is a <c>.cmd</c> shim.
    /// </summary>
    /// <remarks>
    /// Only the extension <c>.cmd</c> counts, compared without regard to case; <c>.bat</c> and <c>.ps1</c> files are
    /// not shims in this sense.
    /// </remarks>
    /// <param name="executablePath">The path of the executable that was found.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="executablePath" /> ends with <c>.cmd</c>; otherwise,
    /// <see langword="false" />.
    /// </returns>
    public static bool IsCmdShim(string executablePath)
    {
        Ensure.NotNull(executablePath);

        return executablePath.EndsWith(ShimExtension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the specified value of the <c>ComSpec</c> environment variable names a usable
    /// <c>cmd.exe</c>.
    /// </summary>
    /// <remarks>
    /// The value is checked with the Windows rules, independent of the host operating system.
    /// </remarks>
    /// <param name="comSpec">The value of the <c>ComSpec</c> environment variable, or <see langword="null" />.</param>
    /// <param name="shimPath">The path of the <c>.cmd</c> shim that is to be run, for the problem sentence.</param>
    /// <param name="problem">
    /// When this method returns, contains a sentence that says why <paramref name="comSpec" /> cannot be used, or
    /// <see langword="null" /> if it can. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="comSpec" /> is a fully qualified path whose file name is
    /// <c>cmd.exe</c> (without regard to case); otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsValidComSpec([NotNullWhen(true)] string? comSpec, string shimPath,
        [NotNullWhen(false)] out string? problem)
    {
        var consequence = $"so the .cmd shim '{shimPath}' cannot be run through cmd.exe.";

        if (string.IsNullOrWhiteSpace(comSpec))
        {
            problem = $"The {ComSpecVariableName} environment variable is not set, {consequence}";
        }
        else if (!PathExecutableSearch.IsWindowsFullyQualified(comSpec))
        {
            problem = $"The {ComSpecVariableName} environment variable '{comSpec}' is not a fully qualified path, " +
                      consequence;
        }
        else if (!string.Equals(GetWindowsFileName(comSpec), CmdExeFileName, StringComparison.OrdinalIgnoreCase))
        {
            problem = $"The {ComSpecVariableName} environment variable '{comSpec}' does not name {CmdExeFileName}, " +
                      consequence;
        }
        else
        {
            problem = null;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks the specified shim path and arguments for characters that <c>cmd.exe</c> could expand or interpret.
    /// </summary>
    /// <remarks>
    /// The path is checked first, then the arguments in order; the first refused character found is reported.
    /// </remarks>
    /// <param name="shimPath">The path of the <c>.cmd</c> shim.</param>
    /// <param name="arguments">The arguments that are to be passed to the shim.</param>
    /// <returns>
    /// A sentence that names the shim path or the 1-based position of the argument, a rendering of it in which
    /// control characters are shown as escapes, the refused character, and why it is refused; or
    /// <see langword="null" /> if nothing is refused.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="shimPath" /> or <paramref name="arguments" /> is <see langword="null" />, or
    /// <paramref name="arguments" /> contains <see langword="null" />.
    /// </exception>
    public static string? FindRefusal(string shimPath, IReadOnlyList<string> arguments)
    {
        Ensure.NotNull(shimPath);
        Ensure.NotNull(arguments);

        var pathIndex = shimPath.AsSpan().IndexOfAny(RefusedCharacterValues);

        if (pathIndex >= 0)
        {
            return $"The path of the .cmd shim '{Render(shimPath)}' contains the character " +
                   $"{DescribeCharacter(shimPath[pathIndex])}, which is refused: {RefusalExplanation}";
        }

        for (var position = 0; position < arguments.Count; position++)
        {
            var argument = Ensure.NotNull(arguments[position]);
            var index = argument.AsSpan().IndexOfAny(RefusedCharacterValues);

            if (index >= 0)
            {
                var number = (position + 1).ToString(CultureInfo.InvariantCulture);

                return $"Argument {number} '{Render(argument)}' contains the character " +
                       $"{DescribeCharacter(argument[index])}, which is refused: {RefusalExplanation}";
            }
        }

        return null;
    }

    /// <summary>
    /// Builds the arguments of <c>cmd.exe</c> that run the specified shim with the specified arguments.
    /// </summary>
    /// <remarks>
    /// The result has the form <c>/d /s /c ""&lt;shim path&gt;" &lt;arguments&gt;"</c>. The arguments are joined with
    /// spaces; an argument that is empty or contains whitespace is enclosed in double quotes, and the backslashes at
    /// its end are doubled, so the program behind the shim does not read the closing quote as an escaped quote. The
    /// result must be passed to <c>cmd.exe</c> verbatim, not as a list of arguments that is quoted again.
    /// </remarks>
    /// <param name="shimPath">The path of the <c>.cmd</c> shim.</param>
    /// <param name="arguments">The arguments that are passed to the shim.</param>
    /// <returns>The verbatim command line arguments of <c>cmd.exe</c>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="shimPath" /> or <paramref name="arguments" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="shimPath" /> or one of <paramref name="arguments" /> contains a refused character; check them
    /// with <see cref="FindRefusal" /> first.
    /// </exception>
    public static string Create(string shimPath, IReadOnlyList<string> arguments)
    {
        var pathRefusal = FindRefusal(shimPath, []);

        if (pathRefusal is not null)
        {
            throw new ArgumentException(pathRefusal, nameof(shimPath));
        }

        var argumentRefusal = FindRefusal(shimPath, arguments);

        if (argumentRefusal is not null)
        {
            throw new ArgumentException(argumentRefusal, nameof(arguments));
        }

        var commandLine = new StringBuilder("/d /s /c \"");

        commandLine.Append(Quote).Append(shimPath).Append(Quote);

        foreach (var argument in arguments)
        {
            commandLine.Append(' ');
            AppendArgument(commandLine, argument);
        }

        return commandLine.Append(Quote).ToString();
    }

    private static string RefusalExplanation =>
        "a .cmd shim is run through cmd.exe, which expands % and ! even inside double quotes and interprets " +
        "\", ^, &, |, <, >, (, ), and line breaks as command syntax, so arguments that contain them are refused " +
        "instead of escaped to rule out command injection.";

    private static void AppendArgument(StringBuilder commandLine, string argument)
    {
        if (argument.Length > 0 && !argument.Any(char.IsWhiteSpace))
        {
            commandLine.Append(argument);

            return;
        }

        var trailingBackslashes = argument.Length - argument.TrimEnd(Backslash).Length;

        commandLine
            .Append(Quote)
            .Append(argument)
            .Append(Backslash, trailingBackslashes)
            .Append(Quote);
    }

    private static string GetWindowsFileName(string path)
    {
        var separatorIndex = path.AsSpan().LastIndexOfAny(Backslash, '/');

        return path[(separatorIndex + 1)..];
    }

    private static string DescribeCharacter(char character)
    {
        return character switch
        {
            '\r' => "carriage return (\\r)",
            '\n' => "line feed (\\n)",
            '\0' => "NUL (\\0)",
            _ => $"'{character}'"
        };
    }

    private static string Render(string text)
    {
        var rendered = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            _ = character switch
            {
                '\r' => rendered.Append("\\r"),
                '\n' => rendered.Append("\\n"),
                '\0' => rendered.Append("\\0"),
                _ when char.IsControl(character) => rendered.Append(CultureInfo.InvariantCulture,
                    $"\\u{(int)character:X4}"),
                _ => rendered.Append(character)
            };
        }

        return rendered.ToString();
    }
}
