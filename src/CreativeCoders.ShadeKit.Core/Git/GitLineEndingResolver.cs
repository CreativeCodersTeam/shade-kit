using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Determines the line ending that the git checkout configuration prescribes for a file by running <c>git</c>
/// through an <see cref="IGitCommandRunner" />.
/// </summary>
/// <remarks>
/// Runs one <c>git check-attr</c> per file, unless <see cref="PreloadAttributesAsync" /> read the attributes, and
/// <c>git config</c> only if the attributes do not decide. Configuration values are cached per repository root for
/// the lifetime of the instance, i.e. one command run; failed or canceled reads are not cached.
/// </remarks>
public sealed class GitLineEndingResolver : IGitLineEndingResolver
{
    private const int SuccessExitCode = 0;

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

    // Keeps the command line of one git check-attr process short, also on Windows.
    private const int MaxPathsPerCheckAttr = 100;

    private static readonly string[] RequestedAttributes = [EolAttribute, TextAttribute, CrLfAttribute];

    // The words that git accepts as boolean true in a configuration value; any integer other than 0 is true as well.
    private static readonly string[] TrueConfigWords = ["true", "yes", "on"];

    private readonly IGitCommandRunner _gitCommandRunner;

    private readonly LineEnding _nativeLineEnding;

    private readonly ConcurrentDictionary<(string RepositoryRootPath, string Key), string?> _configValues =
        new ConcurrentDictionary<(string RepositoryRootPath, string Key), string?>();

    private readonly ConcurrentDictionary<(string RepositoryRootPath, string GitPath), Dictionary<string, string>>
        _preloadedAttributes =
            new ConcurrentDictionary<(string RepositoryRootPath, string GitPath), Dictionary<string, string>>();

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

        // text, crlf, text=auto, or crlf=auto: the line ending comes from eol, core.autocrlf, and core.eol. For auto,
        // git converts only files it detects as text, and SHADE kit writes only text files.
        Text,

        // -text, -crlf, or the binary macro: no conversion.
        Binary,

        // text=input or crlf=input: LF, unless the eol attribute says otherwise.
        Input
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

    /// <inheritdoc />
    [SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly",
        Justification = "An invalid path names the parameter that holds the collection, not the lambda's parameter.")]
    public async Task PreloadAttributesAsync(string repositoryRootPath, IReadOnlyCollection<string> relativePaths,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(relativePaths);

        string[] gitPaths =
        [
            .. relativePaths
                .Select(relativePath => RepositoryFilePath.GetGitPath(repositoryRootPath, relativePath,
                    nameof(relativePaths)))
                .Distinct(StringComparer.Ordinal)
        ];

        var cacheRootPath = gitPaths.Length == 0 ? null : GetCacheRootPath(repositoryRootPath);

        foreach (var chunk in gitPaths.Chunk(MaxPathsPerCheckAttr))
        {
            try
            {
                var attributesByPath = await ReadAttributesAsync(repositoryRootPath, chunk, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var (gitPath, attributes) in attributesByPath)
                {
                    _preloadedAttributes.TryAdd((cacheRootPath!, gitPath), attributes);
                }
            }
            catch (GitUnavailableException)
            {
                // Each file of the chunk reads its attributes when its line ending is determined.
            }
        }
    }

    private async Task<LineEnding?> ResolveAsync(string repositoryRootPath, string gitPath,
        CancellationToken cancellationToken)
    {
        var attributes = _preloadedAttributes.TryGetValue((GetCacheRootPath(repositoryRootPath), gitPath),
                             out var preloadedAttributes)
            ? preloadedAttributes
            : await ReadAttributesAsync(repositoryRootPath, gitPath, cancellationToken).ConfigureAwait(false);

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
        if (textConversion != TextConversion.Text)
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
            SetAttributeValue or AutoAttributeValue => TextConversion.Text,
            UnsetAttributeValue => TextConversion.Binary,
            InputValue => TextConversion.Input,
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

    // The attributes of one file; the path in the output is not compared, so a path that git prints quoted, for
    // example one with non-ASCII characters, is read as well.
    private async Task<Dictionary<string, string>> ReadAttributesAsync(string repositoryRootPath, string gitPath,
        CancellationToken cancellationToken)
    {
        var lines = await RunCheckAttrAsync(repositoryRootPath, [gitPath], cancellationToken).ConfigureAwait(false);

        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (_, attribute, value) in lines)
        {
            attributes[attribute] = value;
        }

        return attributes;
    }

    // The attributes of several files by their path in the output. A path that git prints quoted does not match the
    // requested path, so that file reads its attributes again when its line ending is determined.
    private async Task<Dictionary<string, Dictionary<string, string>>> ReadAttributesAsync(
        string repositoryRootPath, IReadOnlyList<string> gitPaths, CancellationToken cancellationToken)
    {
        var lines = await RunCheckAttrAsync(repositoryRootPath, gitPaths, cancellationToken).ConfigureAwait(false);

        var attributesByPath = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var (path, attribute, value) in lines)
        {
            if (!attributesByPath.TryGetValue(path, out var attributes))
            {
                attributes = new Dictionary<string, string>(StringComparer.Ordinal);
                attributesByPath[path] = attributes;
            }

            attributes[attribute] = value;
        }

        return attributesByPath;
    }

    private async Task<List<(string Path, string Attribute, string Value)>> RunCheckAttrAsync(
        string repositoryRootPath, IReadOnlyList<string> gitPaths, CancellationToken cancellationToken)
    {
        string[] arguments = ["check-attr", .. RequestedAttributes, "--", .. gitPaths];

        var result = await _gitCommandRunner.RunAsync(repositoryRootPath, arguments, cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != SuccessExitCode)
        {
            throw new GitUnavailableException(arguments,
                GitCommandRunnerExtensions.CreateUnexpectedExitCodeReason(result));
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
    private static List<(string Path, string Attribute, string Value)> ParseAttributes(string standardOutput)
    {
        var attributes = new List<(string Path, string Attribute, string Value)>();

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
                attributes.Add((line[..attributeSeparatorIndex], attribute,
                    line[(valueSeparatorIndex + AttributeSeparator.Length)..].Trim()));
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

        var value = await _gitCommandRunner
            .RunForOptionalValueAsync(repositoryRootPath, ["config", key], cancellationToken)
            .ConfigureAwait(false);

        _configValues.TryAdd(cacheKey, value);

        return value;
    }

    // Spellings of the same root, such as a trailing separator, share their values; other spellings, such as a
    // different case on a case-insensitive file system, only read the values once more.
    private static string GetCacheRootPath(string repositoryRootPath)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRootPath));
    }
}
