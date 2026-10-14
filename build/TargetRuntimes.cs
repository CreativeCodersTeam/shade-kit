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

    public static string Host => RuntimeInformation.RuntimeIdentifier;

    public static bool IsWindows(string runtime)
    {
        return runtime.StartsWith("win-", StringComparison.Ordinal);
    }
}
