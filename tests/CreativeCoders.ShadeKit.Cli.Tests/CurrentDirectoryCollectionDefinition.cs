namespace CreativeCoders.ShadeKit.Cli.Tests;

/// <summary>
/// Groups tests that change the process-wide current directory so that they never run in parallel with other tests.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CurrentDirectoryCollectionDefinition
{
    public const string Name = "CurrentDirectory";
}
