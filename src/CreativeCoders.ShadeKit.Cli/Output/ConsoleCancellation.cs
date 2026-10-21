namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Turns the first Ctrl+C (<see cref="Console.CancelKeyPress" />) of a run into a cancellation of
/// <see cref="Token" />.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ListenToCancelKeyPress" /> subscribes to <see cref="Console.CancelKeyPress" />, and
/// <see cref="Dispose" /> unsubscribes again. The first key press cancels <see cref="Token" /> and keeps the process
/// alive, so that the command can stop at its next check and roll back. Every later key press uses the default
/// handling of the runtime, which ends the process, so a command that does not stop can still be terminated.
/// </para>
/// <para>
/// The instance belongs to one run of <see cref="Program" />. A key press after <see cref="Dispose" /> is not handled.
/// </para>
/// </remarks>
public sealed class ConsoleCancellation : ICliCancellation, IDisposable
{
    private readonly Lock _lock = new();

    private readonly CancellationTokenSource _source = new();

    private bool _isDisposed;

    private bool _isListening;

    /// <summary>
    /// Gets the token that is canceled by the first cancel key press.
    /// </summary>
    /// <value>The cancellation token of the run.</value>
    /// <exception cref="ObjectDisposedException">The instance is disposed.</exception>
    public CancellationToken Token => _source.Token;

    /// <summary>
    /// Starts to handle <see cref="Console.CancelKeyPress" /> until the instance is disposed.
    /// </summary>
    /// <remarks>
    /// Calling the method more than once has no further effect.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The instance is disposed.</exception>
    public void ListenToCancelKeyPress()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_isListening)
            {
                return;
            }

            Console.CancelKeyPress += OnCancelKeyPress;
            _isListening = true;
        }
    }

    /// <summary>
    /// Handles one cancel key press and returns whether the runtime must keep the process alive.
    /// </summary>
    /// <remarks>
    /// The first call cancels <see cref="Token" />. Every later call, and every call after <see cref="Dispose" />,
    /// changes nothing. The result is the value for <see cref="ConsoleCancelEventArgs.Cancel" />.
    /// </remarks>
    /// <returns>
    /// <see langword="true" /> if this call canceled <see cref="Token" /> and the process must be kept alive;
    /// otherwise, <see langword="false" />, so that the default handling of the runtime ends the process.
    /// </returns>
    internal bool HandleCancelKeyPress()
    {
        lock (_lock)
        {
            if (_isDisposed || _source.IsCancellationRequested)
            {
                return false;
            }

            _source.Cancel();

            return true;
        }
    }

    /// <summary>
    /// Stops handling <see cref="Console.CancelKeyPress" /> and releases the token source.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_isListening)
            {
                Console.CancelKeyPress -= OnCancelKeyPress;
                _isListening = false;
            }

            _isDisposed = true;
            _source.Dispose();
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = HandleCancelKeyPress();
    }
}
