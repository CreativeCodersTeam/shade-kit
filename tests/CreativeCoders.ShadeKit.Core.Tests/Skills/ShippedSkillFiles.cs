using System.Text;

namespace CreativeCoders.ShadeKit.Core.Tests.Skills;

/// <summary>
/// Finds the shipped skill files of a repository for the guard tests of the skills.
/// </summary>
internal static class ShippedSkillFiles
{
    public const string SkillsDirectoryName = "skills";

    public const string SkillFileName = "SKILL.md";

    private const string SolutionFileName = "ShadeKit.slnx";

    // git treats a file as binary when its first 8000 bytes contain a NUL byte; the guards use the same rule.
    private const int BinaryProbeLength = 8000;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"No directory containing {SolutionFileName} found above {AppContext.BaseDirectory}.");
    }

    /// <summary>
    /// Returns the <c>SKILL.md</c> files under <c>skills/</c>, in ordinal order of their paths. The file name is
    /// compared case-sensitively on every operating system.
    /// </summary>
    public static string[] EnumerateSkillFiles(string repositoryRoot)
    {
        return Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, SkillsDirectoryName), SkillFileName,
                SearchOption.AllDirectories)
            .Where(file => string.Equals(Path.GetFileName(file), SkillFileName, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Returns every text file under <c>skills/</c> with its text (a leading byte order mark removed) and its lines, in
    /// ordinal order of the paths. A file whose first 8000 bytes contain a NUL byte is binary and left out; every other
    /// file must be UTF-8.
    /// </summary>
    /// <exception cref="InvalidDataException">A file that is not binary is not valid UTF-8.</exception>
    public static (string File, string Text, string[] Lines)[] ReadTextFiles(string repositoryRoot)
    {
        return Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, SkillsDirectoryName), "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(file => (File: file, Content: File.ReadAllBytes(file)))
            .Where(x => !IsBinary(x.Content))
            .Select(x => (x.File, Text: Decode(x.File, x.Content)))
            .Select(x => (x.File, x.Text, Lines: SplitLines(x.Text)))
            .ToArray();
    }

    /// <summary>
    /// Returns the lines of the specified text file; a leading byte order mark is removed.
    /// </summary>
    /// <exception cref="InvalidDataException">The file is not valid UTF-8.</exception>
    public static string[] ReadLines(string file)
    {
        return SplitLines(Decode(file, File.ReadAllBytes(file)));
    }

    private static bool IsBinary(byte[] content)
    {
        return content.AsSpan(0, Math.Min(content.Length, BinaryProbeLength)).Contains((byte)0);
    }

    private static string Decode(string file, byte[] content)
    {
        try
        {
            return StrictUtf8.GetString(content).TrimStart('﻿');
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException($"The file '{file}' under skills/ is not valid UTF-8.", ex);
        }
    }

    /// <summary>
    /// Splits the specified text into lines at CRLF, LF, and a lone CR.
    /// </summary>
    public static string[] SplitLines(string text)
    {
        return text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
    }
}
