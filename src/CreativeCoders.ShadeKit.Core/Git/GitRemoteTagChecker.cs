using System.Text.RegularExpressions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Checks whether a tag exists in a remote git repository with <c>git ls-remote</c> through the
/// <see cref="IGitCommandRunner" />.
/// </summary>
/// <remarks>
/// <para>
/// <c>git</c> runs in the home directory of the user (or the temporary directory if the home directory is not known or
/// does not exist), not in the current directory, so the configuration of the current repository takes no part.
/// </para>
/// <para>
/// <c>git</c> never asks for credentials: the runner turns terminal and askpass prompts off, and the call clears the
/// credential helpers (<c>-c credential.helper=</c>) and allows only the <c>https</c> protocol
/// (<c>-c protocol.allow=never -c protocol.https.allow=always</c>), so a URL that the configuration of the user
/// rewrites to <c>ssh</c> fails instead of starting <c>ssh</c>, which could ask for a passphrase. A repository that
/// needs credentials therefore gives <see cref="GitRemoteTagState.Unknown" />.
/// </para>
/// </remarks>
public sealed partial class GitRemoteTagChecker : IGitRemoteTagChecker
{
    private const string TagRefPrefix = "refs/tags/";

    private const string PeeledSuffix = "^{}";

    private readonly IGitCommandRunner _gitCommandRunner;

    private readonly string _workingDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitRemoteTagChecker" /> class that runs <c>git</c> in the home
    /// directory of the user.
    /// </summary>
    /// <param name="gitCommandRunner">The runner that starts <c>git</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="gitCommandRunner" /> is <see langword="null" />.</exception>
    public GitRemoteTagChecker(IGitCommandRunner gitCommandRunner)
        : this(gitCommandRunner, UserWorkingDirectory.Get())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitRemoteTagChecker" /> class that runs <c>git</c> in the
    /// specified directory.
    /// </summary>
    /// <param name="gitCommandRunner">The runner that starts <c>git</c>.</param>
    /// <param name="workingDirectory">The directory in which <c>git</c> runs.</param>
    /// <exception cref="ArgumentNullException"><paramref name="gitCommandRunner" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="workingDirectory" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    internal GitRemoteTagChecker(IGitCommandRunner gitCommandRunner, string workingDirectory)
    {
        _gitCommandRunner = Ensure.NotNull(gitCommandRunner);
        _workingDirectory = Ensure.IsNotNullOrWhitespace(workingDirectory);
    }

    /// <summary>
    /// Gets the directory in which <c>git</c> runs.
    /// </summary>
    /// <value>
    /// The home directory of the user, the temporary directory, or the directory passed to the constructor.
    /// </value>
    internal string WorkingDirectory => _workingDirectory;

    /// <inheritdoc />
    public async Task<GitRemoteTagState> CheckTagAsync(string remoteUrl, string tagName,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(remoteUrl);
        Ensure.NotNull(tagName);

        if (!Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException($"'{remoteUrl}' is not an absolute https:// URL.", nameof(remoteUrl));
        }

        if (!TagNameRegex().IsMatch(tagName))
        {
            throw new ArgumentException(
                $"'{tagName}' is not a tag name that git ls-remote matches exactly: it is empty, starts with '-', " +
                "or contains whitespace, a glob character, '^', '~', or ':'.", nameof(tagName));
        }

        var tagRef = TagRefPrefix + tagName;

        string[] arguments =
        [
            "-c", "credential.helper=", "-c", "protocol.allow=never", "-c", "protocol.https.allow=always",
            "ls-remote", "--tags", remoteUrl, tagRef
        ];

        ExternalProcessResult result;

        try
        {
            result = await _gitCommandRunner.RunAsync(_workingDirectory, arguments, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GitUnavailableException)
        {
            // A missing git (GitNotFoundException) and a git that cannot be started leave the tag unchecked.
            return GitRemoteTagState.Unknown;
        }

        if (result.ExitCode != 0)
        {
            return GitRemoteTagState.Unknown;
        }

        return ContainsTag(result.StandardOutput, tagRef)
            ? GitRemoteTagState.Found
            : GitRemoteTagState.Missing;
    }

    // Each line is "<object id>\t<ref name>"; an annotated tag also has the peeled line "<ref name>^{}".
    private static bool ContainsTag(string standardOutput, string tagRef)
    {
        return standardOutput
            .Split('\n')
            .Select(x => x.TrimEnd('\r').Split('\t', 2))
            .Any(x => x.Length == 2 && (x[1] == tagRef || x[1] == tagRef + PeeledSuffix));
    }

    // '^', '~', and ':' are not allowed in ref names; '^' would also let "<tag>^{}" match the peeled line of <tag>.
    [GeneratedRegex(@"\A[^\s\-*?\[\\^~:][^\s*?\[\\^~:]*\z")]
    private static partial Regex TagNameRegex();
}
