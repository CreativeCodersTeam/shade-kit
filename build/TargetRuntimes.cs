using System.Runtime.InteropServices;

namespace Build;

/// <summary>
/// The runtime identifiers SHADE kit ships self-contained single-file binaries for, grouped by the
/// operating system of the runner that builds them.
/// </summary>
public static class TargetRuntimes
{
    public static IReadOnlyList<string> ForHostOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
        {
            return ["win-x64", "win-arm64"];
        }

        if (OperatingSystem.IsMacOS())
        {
            return ["osx-arm64"];
        }

        return ["linux-x64", "linux-arm64"];
    }

    /// <summary>
    /// The portable runtime identifier of the host (e.g. <c>linux-x64</c>), built from the operating system and the
    /// processor architecture. <see cref="RuntimeInformation.RuntimeIdentifier"/> is not used because a distro-built
    /// SDK reports a distro-specific identifier such as <c>ubuntu.24.04-x64</c>.
    /// </summary>
    public static string Host => $"{HostOperatingSystem}-{RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()}";

    private static string HostOperatingSystem
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return "win";
            }

            return OperatingSystem.IsMacOS() ? "osx" : "linux";
        }
    }

    public static bool IsWindows(string runtime)
    {
        return runtime.StartsWith("win-", StringComparison.Ordinal);
    }
}
