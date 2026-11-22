namespace CreativeCoders.ShadeKit.Core.Versions;

/// <summary>
/// Provides the CLI contract of this engine: the numbered agreement between the engine and the kit skills about the
/// behaviour of the CLI.
/// </summary>
/// <remarks>
/// <para>
/// The CLI contract covers everything the kit skills rely on: the agent-facing commands and their options, the fields
/// of the JSON envelope (<c>CliJsonEnvelope</c>), the fields of its <c>data</c> object, and the error and warning
/// codes. The kit skills state the contract they are written for, and the engine accepts them when that contract lies
/// within <see cref="Window" />.
/// </para>
/// <para>
/// <see cref="Current" /> rises with every change to the contract that the skills can see, additive changes such as
/// a new option or a new <c>data</c> field included. <see cref="MinSupported" /> rises only when a part of the
/// contract is removed or reinterpreted, so that skills written for an older contract would no longer work.
/// </para>
/// <para>
/// The contract numbers are independent of the product version that GitVersion derives from the git history; a new
/// release does not change them unless the contract changes.
/// </para>
/// </remarks>
public static class CliContract
{
    /// <summary>
    /// The newest CLI contract that this engine supports.
    /// </summary>
    /// <remarks>Raised with every change to the contract that the kit skills can see, additive ones included.</remarks>
    public const int Current = 1;

    /// <summary>
    /// The oldest CLI contract that this engine still supports.
    /// </summary>
    /// <remarks>Raised only when a part of the contract is removed or reinterpreted.</remarks>
    public const int MinSupported = 1;

    /// <summary>
    /// Gets the window of CLI contracts that this engine supports.
    /// </summary>
    /// <value>The window from <see cref="MinSupported" /> to <see cref="Current" />.</value>
    public static CliContractWindow Window { get; } = new(MinSupported, Current);
}
