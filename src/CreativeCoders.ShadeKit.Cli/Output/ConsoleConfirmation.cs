using CreativeCoders.Core;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Asks the user to confirm an action through a Spectre.Console yes/no prompt.
/// </summary>
/// <remarks>
/// The prompt reads its answer from the input of the console. Spectre.Console reads keys from the system console, which
/// fails when standard input is redirected, so <see cref="CanAsk" /> also checks
/// <see cref="Console.IsInputRedirected" />.
/// </remarks>
public sealed class ConsoleConfirmation : IConsoleConfirmation
{
    private readonly IAnsiConsole _console;

    private readonly Func<bool> _isInputRedirected;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleConfirmation" /> class that asks on the specified console
    /// and reads whether standard input is redirected from <see cref="Console.IsInputRedirected" />.
    /// </summary>
    /// <param name="console">The console that shows the question and reads the answer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="console" /> is <see langword="null" />.</exception>
    public ConsoleConfirmation(IAnsiConsole console)
        : this(console, static () => Console.IsInputRedirected)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleConfirmation" /> class that asks on the specified console
    /// and reads whether standard input is redirected from the specified function.
    /// </summary>
    /// <remarks>
    /// This constructor is the seam for tests, which cannot redirect the standard input of the test process.
    /// </remarks>
    /// <param name="console">The console that shows the question and reads the answer.</param>
    /// <param name="isInputRedirected">
    /// A function that returns <see langword="true" /> if standard input is redirected; it is called on every read of
    /// <see cref="CanAsk" />.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="console" /> or <paramref name="isInputRedirected" /> is <see langword="null" />.
    /// </exception>
    internal ConsoleConfirmation(IAnsiConsole console, Func<bool> isInputRedirected)
    {
        _console = Ensure.NotNull(console);
        _isInputRedirected = Ensure.NotNull(isInputRedirected);
    }

    /// <inheritdoc />
    public bool CanAsk => !_isInputRedirected() && _console.Profile.Capabilities.Interactive;

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string question, CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(question);

        if (!CanAsk)
        {
            throw new InvalidOperationException(
                "The user cannot be asked because standard input is redirected or the console is not interactive.");
        }

        var prompt = new ConfirmationPrompt(Markup.Escape(question)) { DefaultValue = false };

        return prompt.ShowAsync(_console, cancellationToken);
    }
}
