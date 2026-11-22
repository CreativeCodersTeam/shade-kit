namespace CreativeCoders.ShadeKit.Core.Versions.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the kit skills request a CLI contract that is older than the oldest
/// contract the engine supports.
/// </summary>
/// <remarks>The kit skills are older than the engine supports, so the kit skills must be updated.</remarks>
/// <param name="requestedContract">The CLI contract that the kit skills request.</param>
/// <param name="minSupportedContract">The oldest CLI contract that the engine supports.</param>
/// <param name="currentContract">The newest CLI contract that the engine supports.</param>
public class SkillsContractOutdatedException(int requestedContract, int minSupportedContract, int currentContract)
    : Exception($"The kit skills are written for the CLI contract {requestedContract}, but this engine supports " +
                $"only the CLI contracts {minSupportedContract} to {currentContract}.")
{
    /// <summary>
    /// Gets the CLI contract that the kit skills request.
    /// </summary>
    /// <value>A contract number less than <see cref="MinSupportedContract" />.</value>
    public int RequestedContract { get; } = requestedContract;

    /// <summary>
    /// Gets the oldest CLI contract that the engine supports.
    /// </summary>
    /// <value>The lower bound of the contract window of the engine.</value>
    public int MinSupportedContract { get; } = minSupportedContract;

    /// <summary>
    /// Gets the newest CLI contract that the engine supports.
    /// </summary>
    /// <value>The upper bound of the contract window of the engine.</value>
    public int CurrentContract { get; } = currentContract;
}
