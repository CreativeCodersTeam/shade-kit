using System.Text.RegularExpressions;

namespace CreativeCoders.ShadeKit.Core.Tests.Skills;

/// <summary>
/// Finds the places in a repository working tree that would give Claude Code, GitHub Copilot, or Codex a second source
/// for a skill or for the plugin besides <c>skills/</c> and <c>.claude-plugin/</c> (FP08-AC4).
/// </summary>
/// <remarks>
/// The rules:
/// <list type="number">
/// <item>A skill name (the frontmatter <c>name</c> or the name of the directory of a <c>SKILL.md</c>) belongs to one
/// <c>SKILL.md</c> only.</item>
/// <item>No <c>SKILL.md</c> lies in a repository skill location of an agent (<c>.github/skills/</c>,
/// <c>.agents/skills/</c>, <c>.claude/skills/</c>, <c>.codex/skills/</c>), at any depth.</item>
/// <item>No agent-specific plugin or marketplace manifest lies at the repository root besides
/// <c>.claude-plugin/</c>.</item>
/// <item>No skill folder under <c>skills/</c> holds an agent-specific <c>agents/</c> file (such as Codex's
/// <c>agents/openai.yaml</c>).</item>
/// </list>
/// The file name <c>SKILL.md</c> and the segments of the agent locations, manifests, and exclusions are compared
/// case-insensitively, so that a file that a case-insensitive file system would serve under the looked-up name is
/// flagged too; the <c>skills/</c> directory itself is matched exactly, as in <see cref="ShippedSkillFiles" />.
/// Directory symbolic links are followed, except into a directory that is already on the current path.
/// </remarks>
internal static partial class SingleSkillSourceGuard
{
    private const string SkillFileName = ShippedSkillFiles.SkillFileName;

    private const string SkillsDirectoryName = ShippedSkillFiles.SkillsDirectoryName;

    private const string FrontmatterDelimiter = "---";

    private const string AgentsDirectoryName = "agents";

    // The most links resolved in a row for one path segment before the path counts as a link loop (Linux: 40).
    private const int MaxLinkResolutions = 40;

    // How resolved directory paths are compared for the cycle check; macOS and Windows file systems are
    // case-insensitive by default.
    private static readonly StringComparer RealPathComparer =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    // Build output and tool state of this repository that no agent loads skills from: git's directory, the .NET build
    // output (bin/, obj/, TestResults/), the Cake build output (.artifacts/, .tests/), and the ignored IDE state
    // (.idea/). They are skipped at any depth outside skills/; inside skills/ nothing is skipped, because the plugin
    // ships the whole directory.
    private static readonly string[] ExcludedDirectoryNames =
        [".git", "bin", "obj", "TestResults", ".artifacts", ".tests", ".idea"];

    private static readonly string[][] AgentSkillLocations =
    [
        [".github", SkillsDirectoryName],
        [".agents", SkillsDirectoryName],
        [".claude", SkillsDirectoryName],
        [".codex", SkillsDirectoryName]
    ];

    // The manifest files that Copilot or Codex look up at a plugin or marketplace root before .claude-plugin/.
    private static readonly string[][] AgentManifestFiles =
    [
        ["plugin.json"],
        ["marketplace.json"],
        [".plugin", "plugin.json"],
        [".plugin", "marketplace.json"],
        [".github", "plugin", "plugin.json"],
        [".github", "plugin", "marketplace.json"],
        [".agents", "plugins", "marketplace.json"],
        [".agents", "plugins", "api_marketplace.json"]
    ];

    // Directories at the root that exist only for the manifests of another agent; every file in them is flagged.
    private static readonly string[] AgentManifestDirectories = [".codex-plugin", ".cursor-plugin"];

    private static readonly EnumerationOptions DirectoryEntryOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false
    };

    /// <summary>
    /// Checks the working tree below the specified root against the rules of <see cref="SingleSkillSourceGuard" />.
    /// </summary>
    /// <param name="repositoryRoot">
    /// The root of the working tree, which is also the plugin and marketplace root.
    /// </param>
    /// <returns>
    /// One line <c>&lt;relative path&gt;: &lt;reason&gt;</c> per violation, in ordinal order of the paths and the
    /// reasons.
    /// </returns>
    public static string[] FindViolations(string repositoryRoot)
    {
        var files = EnumerateFiles(repositoryRoot);
        var skillFiles = files.Where(IsSkillFile).ToArray();

        return FindDuplicateSkillNames(repositoryRoot, skillFiles)
            .Concat(FindSkillFilesInAgentLocations(skillFiles))
            .Concat(FindAgentManifests(files))
            .Concat(FindAgentSpecificSkillFiles(files, skillFiles))
            .OrderBy(x => x.Path, StringComparer.Ordinal)
            .ThenBy(x => x.Reason, StringComparer.Ordinal)
            .Select(x => $"{x.Path}: {x.Reason}")
            .ToArray();
    }

    private static IEnumerable<(string Path, string Reason)> FindDuplicateSkillNames(string repositoryRoot,
        string[] skillFiles)
    {
        return skillFiles
            .SelectMany(file => ReadSkillNames(repositoryRoot, file).Select(name => (File: file, Name: name)))
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(x => (x.File,
                Reason: $"skill name '{x.Name}' is also defined by "
                        + string.Join(", ", group.Where(other => other.File != x.File).Select(other => other.File)))));
    }

    private static IEnumerable<(string Path, string Reason)> FindSkillFilesInAgentLocations(string[] skillFiles)
    {
        return skillFiles
            .Select(file => (File: file, Location: FindAgentSkillLocation(Segments(file))))
            .Where(x => x.Location is not null)
            .Select(x => (x.File,
                Reason: $"skill file in the agent skill location '{x.Location}/'; skills live only under "
                        + $"{SkillsDirectoryName}/"));
    }

    private static IEnumerable<(string Path, string Reason)> FindAgentManifests(List<string> files)
    {
        return files
            .Where(file =>
            {
                var segments = Segments(file);

                return AgentManifestFiles.Any(manifest => SegmentsEqual(segments, manifest))
                       || (segments.Length > 1 && AgentManifestDirectories.Contains(segments[0],
                           StringComparer.OrdinalIgnoreCase));
            })
            .Select(file => (file,
                "agent-specific plugin or marketplace manifest; the only manifests are in .claude-plugin/"));
    }

    private static IEnumerable<(string Path, string Reason)> FindAgentSpecificSkillFiles(List<string> files,
        string[] skillFiles)
    {
        var skillDirectories = skillFiles
            .Where(file => IsUnderSkills(Segments(file)))
            .Select(file => Path.GetDirectoryName(file)!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return files
            .Where(file => IsUnderSkills(Segments(file)) && IsInAgentsDirectoryOfSkill(file, skillDirectories))
            .Select(file => (file,
                $"agent-specific skill file; a skill folder holds no {AgentsDirectoryName}/ files in v1"));
    }

    private static bool IsInAgentsDirectoryOfSkill(string file, HashSet<string> skillDirectories)
    {
        for (var directory = Path.GetDirectoryName(file); !string.IsNullOrEmpty(directory);
             directory = Path.GetDirectoryName(directory))
        {
            if (string.Equals(Path.GetFileName(directory), AgentsDirectoryName, StringComparison.OrdinalIgnoreCase)
                && skillDirectories.Contains(Path.GetDirectoryName(directory) ?? string.Empty))
            {
                return true;
            }
        }

        return false;
    }

    // The names a SKILL.md claims: the name of its directory and the frontmatter name, if it has one.
    private static IEnumerable<string> ReadSkillNames(string repositoryRoot, string file)
    {
        var fullPath = Path.Combine(repositoryRoot, file);
        var directoryName = Path.GetFileName(Path.GetDirectoryName(fullPath)!);
        // A link to a missing file claims only its directory name.
        var frontmatterName = TargetExists(fullPath) ? ReadFrontmatterName(File.ReadAllText(fullPath)) : null;

        return new[] { directoryName, frontmatterName }
            .OfType<string>()
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    // File.Exists is true for a dangling link on Unix, so the final target of a link is checked instead.
    private static bool TargetExists(string file)
    {
        var info = new FileInfo(file);

        return info.LinkTarget is null ? info.Exists : info.ResolveLinkTarget(returnFinalTarget: true)?.Exists == true;
    }

    // The file is read leniently: the strict UTF-8 check of skills/ belongs to the other guards, and an undecodable
    // name only falls back to the directory name.
    private static string? ReadFrontmatterName(string text)
    {
        var lines = ShippedSkillFiles.SplitLines(text.TrimStart('﻿'));

        if (lines.Length == 0 || lines[0].TrimEnd() != FrontmatterDelimiter)
        {
            return null;
        }

        var closingIndex = Array.FindIndex(lines, 1, line => line.TrimEnd() == FrontmatterDelimiter);

        if (closingIndex < 0)
        {
            return null;
        }

        return lines[1..closingIndex]
            .Select(line => FrontmatterNameRegex().Match(line))
            .Where(match => match.Success)
            .Select(match => Unquote(match.Groups["value"].Value))
            .FirstOrDefault();
    }

    private static string Unquote(string value)
    {
        return value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0]
            ? value[1..^1]
            : value;
    }

    private static string? FindAgentSkillLocation(string[] segments)
    {
        // The last segment is the file name, so a location must end before it.
        for (var index = 0; index + 1 < segments.Length - 1; index++)
        {
            var location = AgentSkillLocations.FirstOrDefault(x =>
                string.Equals(segments[index], x[0], StringComparison.OrdinalIgnoreCase)
                && string.Equals(segments[index + 1], x[1], StringComparison.OrdinalIgnoreCase));

            if (location is not null)
            {
                return string.Join('/', segments[..(index + 2)]);
            }
        }

        return null;
    }

    private static bool IsSkillFile(string file)
    {
        return string.Equals(Path.GetFileName(file), SkillFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderSkills(string[] segments)
    {
        return segments.Length > 1 && string.Equals(segments[0], SkillsDirectoryName, StringComparison.Ordinal);
    }

    private static bool SegmentsEqual(string[] segments, string[] expected)
    {
        return segments.Length == expected.Length
               && segments.Zip(expected).All(x => string.Equals(x.First, x.Second, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] Segments(string relativePath)
    {
        return relativePath.Split(Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Returns the relative paths of every file below the root, skipping the excluded directories outside
    /// <c>skills/</c>. A directory symbolic link is followed, so a linked copy is found under every path that reaches
    /// it, unless its real target is the directory itself or one of its ancestors, so that a link cycle ends.
    /// </summary>
    private static List<string> EnumerateFiles(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var files = new List<string>();

        CollectFiles(fullRoot, fullRoot, [GetRealPath(fullRoot, 0)], files);

        return files;
    }

    // ancestors holds the real paths of the directory and of the directories above it on the current path.
    private static void CollectFiles(string root, string directory, List<string> ancestors, List<string> files)
    {
        files.AddRange(Directory.EnumerateFiles(directory, "*", DirectoryEntryOptions)
            .Select(file => Path.GetRelativePath(root, file)));

        foreach (var subdirectory in Directory.EnumerateDirectories(directory, "*", DirectoryEntryOptions)
                     .Order(StringComparer.Ordinal))
        {
            var segments = Segments(Path.GetRelativePath(root, subdirectory));

            if (!string.Equals(segments[0], SkillsDirectoryName, StringComparison.Ordinal)
                && ExcludedDirectoryNames.Contains(segments[^1], StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var realPath = GetRealChildPath(ancestors[^1], Path.GetFileName(subdirectory), 0);

            if (ancestors.Contains(realPath, RealPathComparer))
            {
                continue;
            }

            CollectFiles(root, subdirectory, [.. ancestors, realPath], files);
        }
    }

    /// <summary>
    /// Returns the path with every symbolic link in it resolved, segment by segment, as the operating system would
    /// resolve it, so that two spellings of one directory give the same path.
    /// </summary>
    private static string GetRealPath(string fullPath, int resolutions)
    {
        var pathRoot = Path.GetPathRoot(fullPath)!;

        return fullPath[pathRoot.Length..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(pathRoot, (realParent, segment) => GetRealChildPath(realParent, segment, resolutions));
    }

    // The real path of the entry with the specified name in a directory whose path is already real.
    private static string GetRealChildPath(string realParent, string name, int resolutions)
    {
        var path = Path.Combine(realParent, name);
        var linkTarget = new DirectoryInfo(path).LinkTarget;

        if (linkTarget is null)
        {
            return path;
        }

        if (resolutions >= MaxLinkResolutions)
        {
            throw new IOException($"Too many levels of symbolic links at '{path}'.");
        }

        return GetRealPath(Path.GetFullPath(linkTarget, realParent), resolutions + 1);
    }

    [GeneratedRegex(@"^name:[ \t]*(?<value>.*?)[ \t]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FrontmatterNameRegex();
}
