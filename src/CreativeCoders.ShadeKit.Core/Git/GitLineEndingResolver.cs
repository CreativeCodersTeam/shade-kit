using System.Collections.Concurrent;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Determines the line ending that the git checkout configuration prescribes for a file by running <c>git</c>
/// through an <see cref="IGitCommandRunner" />.
/// </summary>
/// <remarks>
/// <para>
/// A call starts one <c>git check-attr</c> process for the attributes <c>eol</c>, <c>text</c>, and <c>crlf</c>, and
/// only if the attributes do not decide, one <c>git config</c> process for <c>core.autocrlf</c> and, if the file is
/// marked as text, one for <c>core.eol</c>.
/// </para>
/// <para>
/// The values of <c>core.autocrlf</c> and <c>core.eol</c> are read at most once per repository root and instance and
/// then kept for the lifetime of the instance, because they are the same for every file of a repository. The class is
/// registered as a singleton and the CLI builds one service container per command run, so the values are read once
/// per command run and a later run reads them again; another container gets another instance with its own values.
/// A value is kept only after <c>git config</c> succeeded or reported the key as not set; a failed or canceled read is
/// not kept. Two calls that run at the same time can both read a value that is not kept yet. The attributes are read
/// for every call, because they depend on the path.
/// </para>
/// </remarks>
public sealed class GitLineEndingResolver : IGitLineEndingResolver
{
    private const int SuccessExitCode = 0;

    private const int NotSetExitCode = 1;

    private const string EolAttribute = "eol";

    private const string TextAttribute = "text";

    private const string CrLfAttribute = "crlf";

    private const string SetAttributeValue = "set";

    private const string UnsetAttributeValue = "unset";

    private const string AutoAttributeValue = "auto";

    private const string LfValue = "lf";

    private const string CrLfValue = "crlf";

    private const string NativeValue = "native";

    private const string InputValue = "input";

    private const string AutoCrLfKey = "core.autocrlf";

    private const string EolKey = "core.eol";

    private const string AttributeSeparator = ": ";

    private static readonly string[] RequestedAttributes = [EolAttribute, TextAttribute, CrLfAttribute];

    // The words that git accepts as boolean true in a configuration value; any integer other than 0 is true as well.
    private static readonly string[] TrueConfigWords = ["true", "yes", "on"];

    private readonly IGitCommandRunner _gitCommandRunner;

    private readonly LineEnding _nativeLineEnding;

    private readonly ConcurrentDictionary<(string RepositoryRootPath, string Key), string?> _configValues =
        new ConcurrentDictionary<(string RepositoryRootPath, string Key), string?>();

    /// <summary>
    /// Initializes a new instance of the <see cref="GitLineEndingResolver" /> class that resolves
    /// <c>core.eol=native</c> to the line ending of the current operating system.
    /// </summary>
    /// <param name="gitCommandRunner">The runner that runs the <c>git</c> commands.</param>
    /// <exception cref="ArgumentNullException"><paramref name="gitCommandRunner" /> is <see langword="null" />.</exception>
    public GitLineEndingResolver(IGitCommandRunner gitCommandRunner)
        : this(gitCommandRunner, OperatingSystemLineEnding.Current)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitLineEndingResolver" /> class with the specified line ending
    /// for <c>core.eol=native</c>.
    /// </summary>
    /// <param name="gitCommandRunner">The runner that runs the <c>git</c> commands.</param>
    /// <param name="nativeLineEnding">
    /// One of the enumeration values that specifies the line ending that <c>core.eol=native</c> stands for.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="gitCommandRunner" /> is <see langword="null" />.</exception>
    internal GitLineEndingResolver(IGitCommandRunner gitCommandRunner, LineEnding nativeLineEnding)
    {
        _gitCommandRunner = Ensure.NotNull(gitCommandRunner);
        _nativeLineEnding = nativeLineEnding;
    }

    // The conversion that the attributes text and crlf ask for, as git's convert_attrs() derives it.
    private enum TextConversion
    {
        // Not specified, or a value that git does not know.
        Undefined,

        // text or crlf: the line ending comes from eol, core.autocrlf, and core.eol.
        Text,

        // -text, -crlf, or the binary macro: no conversion.
        Binary,

        // text=input or crlf=input: LF, unless the eol attribute says otherwise.
        Input,

        // text=auto or crlf=auto: like Text for a file that git detects as text.
        Auto
    }

    /// <inheritdoc />
    public async Task<LineEnding?> GetLineEndingAsync(string repositoryRootPath, string relativePath,
        CancellationToken cancellationToken = default)
    {
        var gitPath = RepositoryFilePath.GetGitPath(repositoryRootPath, relativePath);

        try
        {
            return await ResolveAsync(repositoryRootPath, gitPath, cancellationToken).ConfigureAwait(false);
        }
        catch (GitUnavailableException)
        {
            // git is missing or fails: the line ending is left to .editorconfig and the operating system.
            return null;
        }
    }

    private async Task<LineEnding?> ResolveAsync(string repositoryRootPath, string gitPath,
        CancellationToken cancellationToken)
    {
        var attributes = await ReadAttributesAsync(repositoryRootPath, gitPath, cancellationToken)
            .ConfigureAwait(false);

        // As in git, the legacy crlf attribute counts only while text is unspecified or has an unknown value. The
        // binary macro sets -text, and a later text attribute overrides it, so binary itself is not read.
        var textConversion = GetTextConversion(attributes.GetValueOrDefault(TextAttribute));

        if (textConversion == TextConversion.Undefined)
        {
            textConversion = GetTextConversion(attributes.GetValueOrDefault(CrLfAttribute));
        }

        // A file that is not text is checked out unchanged, even if it also has an eol attribute: eol only implies
        // text while text is unspecified.
        if (textConversion == TextConversion.Binary)
        {
            return null;
        }

        var eolAttributeLineEnding = attributes.GetValueOrDefault(EolAttribute) switch
        {
            LfValue => LineEnding.Lf,
            CrLfValue => LineEnding.CrLf,
            _ => (LineEnding?)null
        };

        if (eolAttributeLineEnding is not null)
        {
            return eolAttributeLineEnding;
        }

        if (textConversion == TextConversion.Input)
        {
            return LineEnding.Lf;
        }

        // A key without a value ("[core] autocrlf") counts as true for git, but git config prints an empty line for
        // it. It is deliberately treated as not set here, so the line ending is left to the next rules.
        var autoCrLf = await ReadConfigValueAsync(repositoryRootPath, AutoCrLfKey, cancellationToken)
            .ConfigureAwait(false);

        if (string.Equals(autoCrLf, InputValue, StringComparison.OrdinalIgnoreCase))
        {
            return LineEnding.Lf;
        }

        if (autoCrLf is not null && IsGitTrue(autoCrLf))
        {
            return LineEnding.CrLf;
        }

        // core.eol only applies to files that are marked as text.
        if (textConversion is not (TextConversion.Text or TextConversion.Auto))
        {
            return null;
        }

        var eol = await ReadConfigValueAsync(repositoryRootPath, EolKey, cancellationToken).ConfigureAwait(false);

        return eol?.ToLowerInvariant() switch
        {
            LfValue => LineEnding.Lf,
            CrLfValue => LineEnding.CrLf,
            NativeValue => _nativeLineEnding,
            _ => null
        };
    }

    // Maps the value of the text or crlf attribute as git's git_path_check_crlf() does; values are case-sensitive.
    private static TextConversion GetTextConversion(string? attributeValue)
    {
        return attributeValue switch
        {
            SetAttributeValue => TextConversion.Text,
            UnsetAttributeValue => TextConversion.Binary,
            InputValue => TextConversion.Input,
            AutoAttributeValue => TextConversion.Auto,
            _ => TextConversion.Undefined
        };
    }

    // git's boolean parsing (git_config_bool): true, yes, and on in any case, and every integer other than 0. false,
    // no, off, 0, and values that git would reject are not true.
    private static bool IsGitTrue(string value)
    {
        return TrueConfigWords.Contains(value, StringComparer.OrdinalIgnoreCase)
               || (TryParseGitInteger(value, out var number) && number != 0);
    }

    // Parses an integer as git_parse_int() does: an optional sign, then a decimal number, a hexadecimal number with
    // 0x, or an octal number with a leading 0 (strtoimax with base 0), then an optional unit k, m, or g in any case
    // (1024, 1024², 1024³). A value outside the range of an int is rejected, as git rejects it.
    private static bool TryParseGitInteger(string value, out long number)
    {
        number = 0;
        var text = value.AsSpan();

        var factor = text.IsEmpty ? 1L : GetUnitFactor(text[^1]);

        if (factor != 1L)
        {
            text = text[..^1];
        }

        var isNegative = !text.IsEmpty && text[0] == '-';

        if (!text.IsEmpty && text[0] is '-' or '+')
        {
            text = text[1..];
        }

        var numberBase = 10;

        if (text.Length > 1 && text[0] == '0' && text[1] is 'x' or 'X')
        {
            numberBase = 16;
            text = text[2..];
        }
        else if (text.Length > 1 && text[0] == '0')
        {
            numberBase = 8;
            text = text[1..];
        }

        if (!TryParseMagnitude(text, numberBase, out var magnitude) || magnitude * factor > int.MaxValue)
        {
            return false;
        }

        number = isNegative ? -(magnitude * factor) : magnitude * factor;

        return true;
    }

    private static long GetUnitFactor(char unit)
    {
        return char.ToLowerInvariant(unit) switch
        {
            'k' => 1024L,
            'm' => 1024L * 1024L,
            'g' => 1024L * 1024L * 1024L,
            _ => 1L
        };
    }

    // Stops at a magnitude above int.MaxValue, so neither the magnitude nor its product with a unit factor overflows.
    private static bool TryParseMagnitude(ReadOnlySpan<char> digits, int numberBase, out long magnitude)
    {
        magnitude = 0;

        if (digits.IsEmpty)
        {
            return false;
        }

        foreach (var digitChar in digits)
        {
            var digit = GetDigitValue(digitChar);

            if (digit < 0 || digit >= numberBase)
            {
                return false;
            }

            magnitude = magnitude * numberBase + digit;

            if (magnitude > int.MaxValue)
            {
                return false;
            }
        }

        return true;
    }

    private static int GetDigitValue(char digit)
    {
        return digit switch
        {
            >= '0' and <= '9' => digit - '0',
            >= 'a' and <= 'f' => digit - 'a' + 10,
            >= 'A' and <= 'F' => digit - 'A' + 10,
            _ => -1
        };
    }

    private async Task<Dictionary<string, string>> ReadAttributesAsync(string repositoryRootPath, string gitPath,
        CancellationToken cancellationToken)
    {
        string[] arguments = ["check-attr", .. RequestedAttributes, "--", gitPath];

        var result = await _gitCommandRunner.RunAsync(repositoryRootPath, arguments, cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != SuccessExitCode)
        {
            throw new GitUnavailableException(arguments, CreateUnexpectedExitCodeReason(result));
        }

        return ParseAttributes(result.StandardOutput);
    }

    /// <summary>
    /// Parses the output of <c>git check-attr</c>, one line <c>&lt;path&gt;: &lt;attribute&gt;: &lt;value&gt;</c>
    /// per attribute.
    /// </summary>
    /// <remarks>
    /// The attribute and the value are taken from the end of the line, because the path may itself contain
    /// <c>": "</c>, while attribute names and values never contain whitespace.
    /// </remarks>
    private static Dictionary<string, string> ParseAttributes(string standardOutput)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in standardOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var valueSeparatorIndex = line.LastIndexOf(AttributeSeparator, StringComparison.Ordinal);

            if (valueSeparatorIndex <= 0)
            {
                continue;
            }

            var attributeSeparatorIndex =
                line.LastIndexOf(AttributeSeparator, valueSeparatorIndex - 1, StringComparison.Ordinal);

            if (attributeSeparatorIndex < 0)
            {
                continue;
            }

            var attribute = line[(attributeSeparatorIndex + AttributeSeparator.Length)..valueSeparatorIndex];

            if (RequestedAttributes.Contains(attribute, StringComparer.Ordinal))
            {
                attributes[attribute] = line[(valueSeparatorIndex + AttributeSeparator.Length)..].Trim();
            }
        }

        return attributes;
    }

    private async Task<string?> ReadConfigValueAsync(string repositoryRootPath, string key,
        CancellationToken cancellationToken)
    {
        var cacheKey = (GetCacheRootPath(repositoryRootPath), key);

        if (_configValues.TryGetValue(cacheKey, out var cachedValue))
        {
            return cachedValue;
        }

        string[] arguments = ["config", key];

        var result = await _gitCommandRunner.RunAsync(repositoryRootPath, arguments, cancellationToken)
            .ConfigureAwait(false);

        var value = result.ExitCode switch
        {
            SuccessExitCode => result.StandardOutput.Trim(),
            NotSetExitCode => null,
            _ => throw new GitUnavailableException(arguments, CreateUnexpectedExitCodeReason(result))
        };

        _configValues.TryAdd(cacheKey, value);

        return value;
    }

    // Spellings of the same root, such as a trailing separator, share their values; other spellings, such as a
    // different case on a case-insensitive file system, only read the values once more.
    private static string GetCacheRootPath(string repositoryRootPath)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRootPath));
    }

    // The exception never leaves this class: GetLineEndingAsync turns it into "git does not decide".
    private static string CreateUnexpectedExitCodeReason(GitCommandResult result)
    {
        return $"git exited with the unexpected exit code {result.ExitCode}.";
    }
}
