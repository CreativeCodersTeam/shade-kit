using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Store;

public class KitStore : IKitStore
{
    private readonly string _kitStorePath;

    public KitStore(string kitStorePath)
    {
        _kitStorePath = Ensure.IsNotNullOrWhitespace(kitStorePath);
    }

    public IEnumerable<string> GetFeatureNames()
    {
        return
        [
            .. Directory.EnumerateDirectories(GetFeaturesPath())
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => !string.IsNullOrWhiteSpace(name))
        ];
    }
    
    private string GetFeaturesPath()
    {
        return Path.Combine(_kitStorePath, KitStorePaths.FeaturesDirName);
    }
}