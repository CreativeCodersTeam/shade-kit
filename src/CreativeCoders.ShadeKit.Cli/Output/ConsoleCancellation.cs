namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Turns the first Ctrl+C (<see cref="Console.CancelKeyPress" />) of a run into a cancellation of
/// <see cref="Token" />.
/// </summary>
/// <remarks>
/// The first key press cancels <see cref="Token" /> and keeps the process alive, so the command can roll back; a
/// later one ends the process with the default handling of the runtime.
/// </remarks>
public sealed class ConsoleCancellation : ICliCancellation, IDisposable
{
    private readonly Lock _lock = new Lock();

    private readonly CancellationTokenSource _source = new CancellationTokenSource();

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
    /// Only the first call before <see cref="Dispose" /> cancels <see cref="Token" />.
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
