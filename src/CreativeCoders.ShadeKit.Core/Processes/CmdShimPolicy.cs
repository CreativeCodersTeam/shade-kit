using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Describes how an <see cref="ExternalTool" /> runs a found Windows <c>.cmd</c> shim through <c>cmd.exe</c>.
/// </summary>
/// <remarks>
/// <para>
/// Setting <see cref="ExternalTool.CmdShims" /> to a policy is the explicit opt-in to running a found <c>.cmd</c>
/// file through <c>cmd.exe</c>, a documented deviation from the "no shell" rule of the process helpers, as described
/// for <see cref="CmdShimCommandLine" />. A tool without a policy starts every found executable directly.
/// </para>
/// <para>
/// The opt-in exists for the GitHub Copilot CLI and the OpenAI Codex CLI, which npm installs as <c>.cmd</c> shims on
/// Windows.
/// </para>
/// </remarks>
/// <param name="CreateRefusedArgumentException">
/// The function that creates the tool's exception from the arguments and the reason when a <c>.cmd</c> shim path or
/// an argument contains a character that <c>cmd.exe</c> would interpret, so the shim is not started.
/// </param>
internal sealed record CmdShimPolicy(
    Func<IReadOnlyList<string>, string, ExternalToolException> CreateRefusedArgumentException);
