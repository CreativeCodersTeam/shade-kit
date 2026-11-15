using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Describes how the <see cref="ExternalProcessRunner" /> starts a process: the executable and either a list of
/// arguments or one verbatim argument string.
/// </summary>
internal sealed class ExternalProcessStart
{
    private ExternalProcessStart(string executablePath, IReadOnlyList<string> arguments, string? verbatimArguments)
    {
        ExecutablePath = executablePath;
        Arguments = arguments;
        VerbatimArguments = verbatimArguments;
    }

    /// <summary>
    /// Gets the path of the executable that is started.
    /// </summary>
    /// <value>The path of the executable, for example the found tool or <c>cmd.exe</c> for a <c>.cmd</c> shim.</value>
    public string ExecutablePath { get; }

    /// <summary>
    /// Gets the arguments that are passed to the process one by one.
    /// </summary>
    /// <value>The arguments; empty if <see cref="VerbatimArguments" /> is set.</value>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets the argument string that is passed to the process unchanged.
    /// </summary>
    /// <value>
    /// The command line after the executable, for example the <c>cmd.exe</c> arguments that run a <c>.cmd</c> shim, or
    /// <see langword="null" /> if <see cref="Arguments" /> are passed instead.
    /// </value>
    public string? VerbatimArguments { get; }

    /// <summary>
    /// Creates a start that passes the specified arguments one by one, without a shell.
    /// </summary>
    /// <param name="executablePath">The path of the executable to start.</param>
    /// <param name="arguments">The arguments that are passed to the process.</param>
    /// <returns>The start of <paramref name="executablePath" /> with <paramref name="arguments" />.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="executablePath" /> or <paramref name="arguments" /> is <see langword="null" />.
    /// </exception>
    public static ExternalProcessStart WithArguments(string executablePath, IReadOnlyList<string> arguments)
    {
        return new ExternalProcessStart(Ensure.NotNull(executablePath), Ensure.NotNull(arguments), null);
    }

    /// <summary>
    /// Creates a start that passes the specified argument string unchanged.
    /// </summary>
    /// <param name="executablePath">The path of the executable to start.</param>
    /// <param name="verbatimArguments">The argument string that is passed to the process unchanged.</param>
    /// <returns>The start of <paramref name="executablePath" /> with <paramref name="verbatimArguments" />.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="executablePath" /> or <paramref name="verbatimArguments" /> is <see langword="null" />.
    /// </exception>
    public static ExternalProcessStart WithVerbatimArguments(string executablePath, string verbatimArguments)
    {
        return new ExternalProcessStart(Ensure.NotNull(executablePath), [], Ensure.NotNull(verbatimArguments));
    }
}
