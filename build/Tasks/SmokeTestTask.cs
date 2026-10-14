using System.Text.Json;
using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using CreativeCoders.CakeBuild.Tasks;
using CreativeCoders.CakeBuild.Tasks.Defaults;

namespace Build.Tasks;

/// <summary>
/// Runs <c>shade-kit version --json</c> with the binary published for the host runtime and checks that it
/// reports the version of this build.
/// </summary>
[TaskName("SmokeTest")]
[IsDependentOn(typeof(PublishTask))]
public class SmokeTestTask : FrostingTaskBase<BuildContext>
{
    protected override Task RunAsyncCore(BuildContext context)
    {
        var runtime = TargetRuntimes.Host;

        if (!context.Runtimes.Contains(runtime))
        {
            context.Information($"Skip smoke test: no binary published for host runtime '{runtime}'");
            return Task.CompletedTask;
        }

        var executable = context.GetPublishDir(runtime)
            .CombineWithFilePath(TargetRuntimes.IsWindows(runtime) ? "shade-kit.exe" : "shade-kit");

        context.Information($"Smoke test: {executable.FullPath} version --json");

        var exitCode = context.StartProcess(executable,
            new ProcessSettings { Arguments = "version --json", RedirectStandardOutput = true },
            out var standardOutput);

        var output = string.Join(Environment.NewLine, standardOutput);
        context.Information(output);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Smoke test failed: '{executable}' exited with code {exitCode}");
        }

        var engineVersion = ReadEngineVersion(output);
        var expectedVersion = context.Version.SemVer;

        if (engineVersion != expectedVersion)
        {
            throw new InvalidOperationException(
                $"Smoke test failed: binary reports engine version '{engineVersion}', expected '{expectedVersion}'");
        }

        context.Information($"Smoke test passed: engine version {engineVersion}");

        return Task.CompletedTask;
    }

    private static string? ReadEngineVersion(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty("data").GetProperty("engineVersion").GetString();
    }
}
