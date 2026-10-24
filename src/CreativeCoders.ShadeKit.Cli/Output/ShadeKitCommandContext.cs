using CreativeCoders.Cli.Hosting.Exceptions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Bundles the services that every agent-facing command uses to run its core operation and to report the result or an
/// expected failure as plain text or as one JSON envelope.
/// </summary>
/// <remarks>
/// A command that runs and writes through this context writes exactly one envelope in JSON mode: a failure
/// envelope for an expected failure, otherwise a success envelope.
/// </remarks>
/// <param name="ansiConsole">The console that receives the plain output.</param>
/// <param name="kitStoreFactory">The factory that locates the SHADE kit store of the current repository.</param>
/// <param name="jsonWriter">The writer that receives the JSON output.</param>
/// <param name="errorMapper">The mapper that turns failures into exit codes, error codes, and messages.</param>
/// <param name="cancellation">The cancellation of the run, whose token is passed to the core.</param>
public sealed class ShadeKitCommandContext(
    IAnsiConsole ansiConsole,
    IKitStoreFactory kitStoreFactory,
    ICliJsonWriter jsonWriter,
    CliErrorMapper errorMapper,
    ICliCancellation cancellation)
{
    private readonly IKitStoreFactory _kitStoreFactory = Ensure.NotNull(kitStoreFactory);

    private readonly ICliJsonWriter _jsonWriter = Ensure.NotNull(jsonWriter);

    private readonly CliErrorMapper _errorMapper = Ensure.NotNull(errorMapper);

    private readonly ICliCancellation _cancellation = Ensure.NotNull(cancellation);

    /// <summary>
    /// Gets the console that receives the plain output.
    /// </summary>
    /// <value>The console of the run.</value>
    public IAnsiConsole Console { get; } = Ensure.NotNull(ansiConsole);

    /// <summary>
    /// Gets the token that is canceled when the user cancels the run.
    /// </summary>
    /// <value>The cancellation token of the run.</value>
    public CancellationToken CancellationToken => _cancellation.Token;

    /// <summary>
    /// Creates the SHADE kit store of the git repository that contains the current directory and aborts the command if
    /// no store is found.
    /// </summary>
    /// <param name="json">
    /// <see langword="true" /> if the command runs in JSON mode; otherwise, <see langword="false" />.
    /// </param>
    /// <returns>The store of the <c>.shadekit</c> directory at the repository root.</returns>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found (exit codes <see cref="ShadeKitCliExitCodes.NotAGitRepository" />,
    /// <see cref="ShadeKitCliExitCodes.KitRootNotFound" />, or
    /// <see cref="ShadeKitCliExitCodes.KitRootNotADirectory" />).
    /// </exception>
    public IKitStore CreateKitStore(bool json)
    {
        return CliCommandAbort.Run(_kitStoreFactory.Create, _errorMapper, json, _jsonWriter);
    }

    /// <summary>
    /// Runs an asynchronous core operation with the cancellation token of the run and aborts the command if the
    /// operation fails with an expected failure.
    /// </summary>
    /// <typeparam name="T">The type of the result of the operation.</typeparam>
    /// <param name="json">
    /// <see langword="true" /> if the command runs in JSON mode; otherwise, <see langword="false" />.
    /// </param>
    /// <param name="operation">The operation to run; it receives <see cref="CancellationToken" />.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the result of
    /// <paramref name="operation" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// <paramref name="operation" /> threw an exception that the <see cref="CliErrorMapper" /> maps; it is reported
    /// as <see cref="CliCommandAbort.Create" /> describes. Any other exception escapes unchanged.
    /// </exception>
    public Task<T> RunAsync<T>(bool json, Func<CancellationToken, Task<T>> operation)
    {
        Ensure.NotNull(operation);

        return CliCommandAbort.RunAsync(() => operation(_cancellation.Token), _errorMapper, json, _jsonWriter);
    }

    /// <summary>
    /// Creates the SHADE kit store of the git repository that contains the current directory, runs an asynchronous
    /// core operation on it with the cancellation token of the run, and aborts the command if the store is not found or
    /// the operation fails with an expected failure.
    /// </summary>
    /// <typeparam name="T">The type of the result of the operation.</typeparam>
    /// <param name="json">
    /// <see langword="true" /> if the command runs in JSON mode; otherwise, <see langword="false" />.
    /// </param>
    /// <param name="operation">
    /// The operation to run; it receives the created store and <see cref="CancellationToken" />. It is not called if
    /// the store cannot be created.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the result of
    /// <paramref name="operation" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation" /> is <see langword="null" />.</exception>
    /// <exception cref="CliCommandAbortException">
    /// No SHADE kit store is found (exit codes <see cref="ShadeKitCliExitCodes.NotAGitRepository" />,
    /// <see cref="ShadeKitCliExitCodes.KitRootNotFound" />, or
    /// <see cref="ShadeKitCliExitCodes.KitRootNotADirectory" />), or <paramref name="operation" /> threw an exception
    /// that the <see cref="CliErrorMapper" /> maps; it is reported as <see cref="CliCommandAbort.Create" /> describes.
    /// Any other exception escapes unchanged.
    /// </exception>
    public Task<T> RunAsync<T>(bool json, Func<IKitStore, CancellationToken, Task<T>> operation)
    {
        Ensure.NotNull(operation);

        return CliCommandAbort.RunAsync(() => operation(_kitStoreFactory.Create(), _cancellation.Token), _errorMapper,
            json, _jsonWriter);
    }

    /// <summary>
    /// Writes the successful result of a command without warnings as one JSON envelope or as plain text.
    /// </summary>
    /// <remarks>
    /// Behaves like <see cref="WriteSuccess(bool, object, IEnumerable{KitWarning}, Action)" /> with no warnings.
    /// </remarks>
    /// <param name="json">
    /// <see langword="true" /> if the command runs in JSON mode; otherwise, <see langword="false" />.
    /// </param>
    /// <param name="data">The <c>data</c> object of the envelope.</param>
    /// <param name="writePlain">The action that writes the plain result lines.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data" /> or <paramref name="writePlain" /> is <see langword="null" />.
    /// </exception>
    public void WriteSuccess(bool json, object data, Action writePlain)
    {
        WriteSuccess(json, data, [], writePlain);
    }

    /// <summary>
    /// Writes the successful result of a command as one JSON envelope or as plain text after the warnings.
    /// </summary>
    /// <remarks>
    /// In plain mode, each warning is written as a line <c>Warning: &lt;message&gt;</c> before the result lines.
    /// </remarks>
    /// <param name="json">
    /// <see langword="true" /> if the command runs in JSON mode; otherwise, <see langword="false" />.
    /// </param>
    /// <param name="data">The <c>data</c> object of the envelope.</param>
    /// <param name="warnings">The warnings of the core result, in the order in which they are reported.</param>
    /// <param name="writePlain">The action that writes the plain result lines.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data" />, <paramref name="warnings" />, or <paramref name="writePlain" /> is
    /// <see langword="null" />.
    /// </exception>
    public void WriteSuccess(bool json, object data, IEnumerable<KitWarning> warnings, Action writePlain)
    {
        Ensure.NotNull(data);
        Ensure.NotNull(warnings);
        Ensure.NotNull(writePlain);

        CliJsonMessage[] messages = [.. warnings.Select(CliErrorMapper.MapWarning)];

        if (json)
        {
            _jsonWriter.Write(CliJsonEnvelope.Success(data, messages));

            return;
        }

        foreach (var message in messages)
        {
            Console.WriteLine($"Warning: {message.Message}");
        }

        writePlain();
    }
}
