using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Claude;
using CreativeCoders.SysConsole.Core;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Integrations;

/// <summary>
/// Writes the results of the <c>shade-kit integrations</c> commands, and the installed integrations that
/// <c>shade-kit version</c> lists, as Spectre.Console markup for the plain output.
/// </summary>
/// <remarks>
/// Every method escapes the values it formats, so its result can be written with <c>MarkupLine</c> as it is.
/// </remarks>
internal static class IntegrationsTextFormat
{
    /// <summary>
    /// Represents the line written when an update finds no installed integration.
    /// </summary>
    public const string NothingInstalledText = "No SHADE kit integration is installed.";

    /// <summary>
    /// Represents the line written after the integration of Claude Code was installed or updated.
    /// </summary>
    public const string RestartClaudeText = "Restart Claude Code to load the skills.";

    /// <summary>
    /// Writes one line per agent of the specified run, a note for every disabled integration, and the hint to restart
    /// Claude Code if its integration was installed or updated.
    /// </summary>
    /// <remarks>
    /// A run without results or without a source writes <see cref="NothingInstalledText" /> and a hint to
    /// <c>shade-kit integrations install</c>.
    /// </remarks>
    /// <param name="console">The console that receives the lines.</param>
    /// <param name="result">The result of the install or update.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="console" /> or <paramref name="result" /> is <see langword="null" />.
    /// </exception>
    public static void WriteRun(IAnsiConsole console, IntegrationRunResult result)
    {
        Ensure.NotNull(console);
        Ensure.NotNull(result);

        if (result.Integrations.Count == 0 || result.Source is not { } source)
        {
            console.MarkupLine(NothingInstalledText.ToEscapedMarkup().ToWarningMarkup());
            console.MarkupLine(
                $"Run '{"shade-kit integrations install".ToCommandMarkup()}' to install it.");

            return;
        }

        foreach (var integration in result.Integrations)
        {
            console.MarkupLine(FormatResult(integration, source));

            if (!integration.Enabled)
            {
                console.MarkupLine($"  {FormatDisabledNote(integration.AgentId, integration.PluginId)}");
            }
        }

        if (result.Integrations.Any(x => x.Outcome != IntegrationOutcome.Current &&
                                         x.AgentId == ClaudeCodeIntegration.ClaudeAgentId))
        {
            console.MarkupLine(RestartClaudeText.ToEscapedMarkup().ToMutedMarkup());
        }
    }

    /// <summary>
    /// Formats the result of one agent of an install or update.
    /// </summary>
    /// <param name="result">The result of the agent.</param>
    /// <param name="source">The marketplace source that the engine targeted.</param>
    /// <returns>
    /// The markup of the text <c>&lt;agent&gt;: &lt;outcome&gt; &lt;plugin id&gt; &lt;version&gt;</c>, for example
    /// <c>claude: installed shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)</c>,
    /// <c>claude: updated shade-kit@creativecoders-shade-kit 0.7.0 -&gt; 0.8.0 (v0.8.0)</c>, or
    /// <c>claude: already current shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)</c>. Without a ref the version is
    /// <c>from &lt;path&gt;</c> for a local directory and <c>(default branch)</c> for a GitHub repository.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="result" /> or <paramref name="source" /> is <see langword="null" />.
    /// </exception>
    public static string FormatResult(IntegrationResult result, MarketplaceSource source)
    {
        Ensure.NotNull(result);
        Ensure.NotNull(source);

        var version = FormatVersion(result.KitVersion, result.Ref,
            result.Ref is null ? FormatSourceWithoutRef(source) : string.Empty);

        if (result is { Outcome: IntegrationOutcome.Updated, PreviousKitVersion: { } previousKitVersion })
        {
            version = $"{previousKitVersion.ToEscapedMarkup()} -> {version}";
        }

        var outcome = result.Outcome switch
        {
            IntegrationOutcome.Installed => "installed".ToSuccessMarkup(),
            IntegrationOutcome.Updated => "updated".ToSuccessMarkup(),
            IntegrationOutcome.Current => "already current".ToMutedMarkup(),
            _ => result.Outcome.ToString().ToEscapedMarkup()
        };

        return JoinParts($"{result.AgentId.ToEscapedMarkup().ToBoldMarkup()}:", outcome,
            result.PluginId.ToEscapedMarkup(), version);
    }

    /// <summary>
    /// Formats an integration that is installed in an agent, as <c>shade-kit version</c> lists it.
    /// </summary>
    /// <param name="integration">The installed integration.</param>
    /// <returns>
    /// The markup of the text <c>&lt;agent&gt;: &lt;plugin id&gt; &lt;version&gt;</c>, for example
    /// <c>claude: shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)</c>, followed by <c>(disabled)</c> if the
    /// integration is disabled.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="integration" /> is <see langword="null" />.</exception>
    public static string FormatInstalled(InstalledIntegration integration)
    {
        Ensure.NotNull(integration);

        return JoinParts($"{integration.AgentId.ToEscapedMarkup().ToBoldMarkup()}:",
            integration.PluginId.ToEscapedMarkup(),
            FormatVersion(integration.KitVersion, integration.Ref, string.Empty),
            integration.Enabled ? string.Empty : "(disabled)".ToWarningMarkup());
    }

    /// <summary>
    /// Formats the note that an integration is disabled and how to enable it.
    /// </summary>
    /// <param name="agentId">The id of the agent.</param>
    /// <param name="pluginId">The id of the integration in the agent.</param>
    /// <returns>
    /// The markup of a sentence that names <c>claude plugin enable &lt;plugin id&gt;</c> for Claude Code, or that
    /// says to enable the integration in the agent for any other agent.
    /// </returns>
    public static string FormatDisabledNote(string agentId, string pluginId)
    {
        if (agentId == ClaudeCodeIntegration.ClaudeAgentId)
        {
            var command = $"claude plugin enable {pluginId}".ToEscapedMarkup().ToCommandMarkup();

            return $"{"The plugin is disabled in Claude Code.".ToWarningMarkup()} Run '{command}' to enable it.";
        }

        return $"The integration is disabled in the agent '{agentId.ToEscapedMarkup()}'; enable it there."
            .ToWarningMarkup();
    }

    // The kit version comes first, then the ref in parentheses, or the markup that stands for a missing ref.
    private static string FormatVersion(string? kitVersion, string? @ref, string missingRefMarkup)
    {
        return JoinParts(kitVersion?.ToEscapedMarkup() ?? string.Empty,
            @ref is null ? missingRefMarkup : $"({@ref.ToEscapedMarkup()})");
    }

    // A source without a ref is a local directory, named by its path, or the default branch of a GitHub repository.
    private static string FormatSourceWithoutRef(MarketplaceSource source)
    {
        return source.Path is { } path
            ? $"from {path.ToEscapedMarkup()}"
            : "(default branch)";
    }

    private static string JoinParts(params string[] parts)
    {
        return string.Join(' ', parts.Where(x => x.Length > 0));
    }
}
