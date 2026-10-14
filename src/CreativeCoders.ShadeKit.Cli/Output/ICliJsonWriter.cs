namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Writes the JSON output of a command.
/// </summary>
public interface ICliJsonWriter
{
    /// <summary>
    /// Writes the specified envelope as one JSON document.
    /// </summary>
    /// <param name="envelope">The envelope to write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="envelope" /> is <see langword="null" />.</exception>
    void Write(CliJsonEnvelope envelope);
}
