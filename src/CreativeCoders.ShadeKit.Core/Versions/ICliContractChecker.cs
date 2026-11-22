using CreativeCoders.ShadeKit.Core.Versions.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Versions;

/// <summary>
/// Checks whether the engine supports the CLI contract that the kit skills request.
/// </summary>
public interface ICliContractChecker
{
    /// <summary>
    /// Gets the window of CLI contracts that the checker accepts.
    /// </summary>
    /// <remarks>
    /// <c>shade-kit version</c> reports this window, so the reported window and the checked window are the same value.
    /// </remarks>
    /// <value>The window that <see cref="Check" /> checks a requested contract against.</value>
    CliContractWindow Window { get; }

    /// <summary>
    /// Checks the specified requested CLI contract against the contract window of the engine.
    /// </summary>
    /// <remarks>
    /// The check succeeds when <paramref name="requestedContract" /> lies within the window, bounds included:
    /// <c>MinSupported &lt;= requestedContract &lt;= Current</c>. There is no leniency for prerelease engines and no
    /// warning; a requested contract outside the window always fails. Contracts start at 1, so a requested contract 0
    /// is valid input that always lies below the window.
    /// </remarks>
    /// <param name="requestedContract">The CLI contract that the kit skills are written for.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="requestedContract" /> is negative.
    /// </exception>
    /// <exception cref="EngineContractOutdatedException">
    /// <paramref name="requestedContract" /> is greater than the current contract of the engine, so the kit skills
    /// are newer than the engine.
    /// </exception>
    /// <exception cref="SkillsContractOutdatedException">
    /// <paramref name="requestedContract" /> is less than the oldest contract that the engine supports, so the kit
    /// skills are older than the engine supports.
    /// </exception>
    void Check(int requestedContract);
}
