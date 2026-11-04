using System.Text.Json;
using System.Text.Json.Serialization;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Represents the gate configuration stored in <c>.shadekit/gates.json</c>.
/// </summary>
/// <remarks>
/// The team owns the values; the engine owns the schema (format version <c>1</c>). The gates are keyed by workflow
/// profile and then by the target phase of the transition. Unknown fields are kept in
/// <see cref="VersionedDocument.ExtensionData" /> and <see cref="GateConfig.ExtensionData" />; unknown profile and
/// phase names are kept as well and do not make the file invalid. A profile or phase name with a control character
/// makes the file invalid.
/// </remarks>
public sealed class GatesConfig : VersionedDocument, IJsonOnDeserializing, IJsonOnDeserialized
{
    /// <summary>
    /// Represents the path of the Definition of Ready checklist, the checklist of the gate into <c>plan</c>.
    /// </summary>
    public const string DefinitionOfReadyChecklist = ChecklistDirectory + "definition-of-ready.md";

    /// <summary>
    /// Represents the path of the Definition of Done checklist, the checklist of the gate into <c>finish</c>.
    /// </summary>
    public const string DefinitionOfDoneChecklist = ChecklistDirectory + "definition-of-done.md";

    private const string ChecklistDirectory = KitStorePaths.ChecksDirName + "/";

    private const string ChecklistExtension = ".md";

    private Dictionary<string, Dictionary<string, GateConfig>>? _profiles;

    private bool _profilesAssigned;

    /// <summary>
    /// Gets or sets the gates of every workflow profile.
    /// </summary>
    /// <value>
    /// The gates keyed by profile name and then by the name of the target phase, or <see langword="null" /> if the
    /// field is missing. An empty mapping is kept apart from a missing field. A <see langword="null" /> value is not
    /// written; an explicit <see langword="null" /> field, profile, or gate makes the file invalid.
    /// </value>
    public Dictionary<string, Dictionary<string, GateConfig>>? Profiles
    {
        get => _profiles;
        set
        {
            _profiles = value;
            _profilesAssigned = true;
        }
    }

    /// <summary>
    /// Creates the gate configuration that <c>shade-kit init</c> writes.
    /// </summary>
    /// <returns>
    /// A new gate configuration in which every transition of the built-in profiles needs an approval, except the
    /// <c>quick</c> profile's transition into <c>quick</c>; the gates into <c>plan</c> have the Definition of Ready
    /// checklist and the gates into <c>finish</c> the Definition of Done checklist.
    /// </returns>
    public static GatesConfig CreateDefault()
    {
        return new GatesConfig
        {
            Profiles = new Dictionary<string, Dictionary<string, GateConfig>>
            {
                [WorkflowProfileNames.Feature] = new Dictionary<string, GateConfig>
                {
                    [WorkflowPhaseNames.Refine] = Approved(),
                    [WorkflowPhaseNames.Map] = Approved(),
                    [WorkflowPhaseNames.Plan] = Approved(DefinitionOfReadyChecklist),
                    [WorkflowPhaseNames.Implement] = Approved(),
                    [WorkflowPhaseNames.Verify] = Approved(),
                    [WorkflowPhaseNames.Finish] = Approved(DefinitionOfDoneChecklist)
                },
                [WorkflowProfileNames.Bugfix] = new Dictionary<string, GateConfig>
                {
                    [WorkflowPhaseNames.Diagnose] = Approved(),
                    [WorkflowPhaseNames.Plan] = Approved(DefinitionOfReadyChecklist),
                    [WorkflowPhaseNames.Implement] = Approved(),
                    [WorkflowPhaseNames.Verify] = Approved(),
                    [WorkflowPhaseNames.Finish] = Approved(DefinitionOfDoneChecklist)
                },
                [WorkflowProfileNames.Quick] = new Dictionary<string, GateConfig>
                {
                    [WorkflowPhaseNames.Quick] = new GateConfig { Approval = false },
                    [WorkflowPhaseNames.Finish] = Approved(DefinitionOfDoneChecklist)
                }
            }
        };

        static GateConfig Approved(string? checklist = null)
        {
            return new GateConfig { Approval = true, Checklist = checklist };
        }
    }

    void IJsonOnDeserializing.OnDeserializing()
    {
        _profilesAssigned = false;
    }

    void IJsonOnDeserialized.OnDeserialized()
    {
        if (_profiles is null)
        {
            // The serializer assigns the property only when the field is present, so a missing field stays apart.
            if (_profilesAssigned)
            {
                throw new JsonException("The 'profiles' field is null.");
            }

            return;
        }

        foreach (var (profileName, gates) in _profiles)
        {
            if (ContainsControlCharacter(profileName))
            {
                throw new JsonException(
                    $"The profile name '{ToPrintable(profileName)}' in the 'profiles' field contains a control " +
                    "character.");
            }

            // RespectNullableAnnotations does not cover dictionary values, so null entries are rejected here.
            if (gates is null)
            {
                throw new JsonException($"The profile '{profileName}' in the 'profiles' field is null.");
            }

            foreach (var (phaseName, gate) in gates)
            {
                ValidateGate(profileName, phaseName, gate);
            }
        }
    }

    private static void ValidateGate(string profileName, string phaseName, GateConfig? gate)
    {
        if (ContainsControlCharacter(phaseName))
        {
            throw new JsonException(
                $"The phase name '{ToPrintable(phaseName)}' of the profile '{profileName}' in the 'profiles' field " +
                "contains a control character.");
        }

        if (gate is null)
        {
            throw new JsonException(
                $"The gate '{phaseName}' of the profile '{profileName}' in the 'profiles' field is null.");
        }

        if (gate.Checklist is { } checklist && !IsValidChecklistPath(checklist))
        {
            throw new JsonException(
                $"The checklist '{ToPrintable(checklist)}' of the gate '{phaseName}' of the profile '{profileName}' " +
                $"must be a relative path with '/' separators that starts with '{ChecklistDirectory}' and ends with " +
                $"'{ChecklistExtension}' after a file name, without '\\', ':', control characters, and empty, '.', " +
                "or '..' segments.");
        }
    }

    /// <summary>
    /// Determines whether the specified value is a valid <see cref="GateConfig.Checklist" /> path.
    /// </summary>
    /// <param name="checklist">The checklist path to check.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="checklist" /> is a relative path with <c>/</c> separators that
    /// starts with <c>checks/</c>, ends with <c>.md</c> after a file name, and has no <c>\</c>, no <c>:</c>, no
    /// control characters, and no empty, <c>.</c>, or <c>..</c> segments; otherwise, <see langword="false" />.
    /// </returns>
    /// <remarks>
    /// A <c>:</c> would name an alternate data stream on Windows, for example <c>checks/x.md:hidden.md</c>; a control
    /// character could reach the terminal through the output of the commands.
    /// </remarks>
    internal static bool IsValidChecklistPath(string checklist)
    {
        if (!checklist.StartsWith(ChecklistDirectory, StringComparison.Ordinal)
            || !checklist.EndsWith(ChecklistExtension, StringComparison.Ordinal)
            || checklist.Contains('\\', StringComparison.Ordinal)
            || checklist.Contains(':', StringComparison.Ordinal)
            || ContainsControlCharacter(checklist))
        {
            return false;
        }

        var segments = checklist.Split('/');

        // The file name needs a name in front of the extension; ".md" alone names no checklist.
        return segments.All(segment => segment is not ("" or "." or ".."))
               && segments[^1] != ChecklistExtension;
    }

    private static bool ContainsControlCharacter(string value)
    {
        return value.Any(char.IsControl);
    }

    // Renders control characters as \uXXXX escapes, so the message of an invalid file never carries them to the
    // terminal or the agent.
    private static string ToPrintable(string value)
    {
        return ContainsControlCharacter(value)
            ? string.Concat(value.Select(c => char.IsControl(c) ? $"\\u{(int)c:x4}" : new string(c, 1)))
            : value;
    }
}
