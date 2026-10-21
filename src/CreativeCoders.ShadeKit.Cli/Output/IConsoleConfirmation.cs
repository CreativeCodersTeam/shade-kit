namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Asks the user on the console to confirm an action.
/// </summary>
public interface IConsoleConfirmation
{
    /// <summary>
    /// Gets a value that indicates whether the user can be asked on the console.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if standard input is not redirected and the console is interactive; otherwise,
    /// <see langword="false" />.
    /// </value>
    bool CanAsk { get; }

    /// <summary>
    /// Asks the user the specified yes/no question and waits for the answer.
    /// </summary>
    /// <remarks>
    /// The default answer is <em>no</em>: pressing Enter without an answer declines. The question is shown as plain
    /// text; markup characters in it are not interpreted. Callers check <see cref="CanAsk" /> first.
    /// </remarks>
    /// <param name="question">The question to show, without the choices.</param>
    /// <param name="cancellationToken">A token to cancel the wait for the answer.</param>
    /// <returns>
    /// A task that represents the question. The task result is <see langword="true" /> if the user answered yes;
    /// otherwise, <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="question" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException"><see cref="CanAsk" /> is <see langword="false" />.</exception>
    Task<bool> ConfirmAsync(string question, CancellationToken cancellationToken = default);
}
