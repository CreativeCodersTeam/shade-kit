using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Reads information about a git repository and its configuration by running <c>git</c> through an
/// <see cref="IGitCommandRunner" />.
/// </summary>
/// <param name="gitCommandRunner">The runner that runs the <c>git</c> commands.</param>
public sealed class GitRepositoryInfo(IGitCommandRunner gitCommandRunner) : IGitRepositoryInfo
{
    private const string UserNameKey = "user.name";

    private const string UserEmailKey = "user.email";

    private static readonly string[] CurrentBranchArguments = ["symbolic-ref", "--short", "-q", "HEAD"];

    private readonly IGitCommandRunner _gitCommandRunner = Ensure.NotNull(gitCommandRunner);

    /// <inheritdoc />
    public async Task<string?> GetCurrentBranchAsync(string repositoryRootPath,
        CancellationToken cancellationToken = default)
    {
        Ensure.IsNotNullOrWhitespace(repositoryRootPath);

        return await _gitCommandRunner.RunForOptionalValueAsync(repositoryRootPath, CurrentBranchArguments, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GitIdentity> GetIdentityAsync(string repositoryRootPath,
        CancellationToken cancellationToken = default)
    {
        Ensure.IsNotNullOrWhitespace(repositoryRootPath);

        // Both keys are read at the same time. Task.WhenAll throws the first exception in task order, so a failure of
        // the name read wins over a failure of the email read; a cancellation is thrown only if neither read failed.
        var nameTask =
            _gitCommandRunner.RunForOptionalValueAsync(repositoryRootPath, ["config", UserNameKey], cancellationToken);
        var emailTask =
            _gitCommandRunner.RunForOptionalValueAsync(repositoryRootPath, ["config", UserEmailKey], cancellationToken);

        await Task.WhenAll(nameTask, emailTask).ConfigureAwait(false);

        var name = await nameTask.ConfigureAwait(false);
        var email = await emailTask.ConfigureAwait(false);

        if (name is not null && email is not null)
        {
            return new GitIdentity(name, email);
        }

        List<string> missingKeys = [];

        if (name is null)
        {
            missingKeys.Add(UserNameKey);
        }

        if (email is null)
        {
            missingKeys.Add(UserEmailKey);
        }

        throw new GitIdentityNotConfiguredException(repositoryRootPath, missingKeys);
    }
}
