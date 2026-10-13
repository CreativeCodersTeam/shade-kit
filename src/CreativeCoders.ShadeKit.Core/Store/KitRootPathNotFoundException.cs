namespace CreativeCoders.ShadeKit.Core.Store;

public class KitRootPathNotFoundException(string path) : Exception
{
    public string StartPath { get; } = path;
}
