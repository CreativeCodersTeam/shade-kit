namespace CreativeCoders.ShadeKit.Core.Store;

public class KitStoreFactory : IKitStoreFactory
{
    public IKitStore Create()
    {
        return Create(FindKitRootPathRecursively(Directory.GetCurrentDirectory()));
    }

    public IKitStore Create(string path)
    {
        var kitStorePath = FindKitRootPathRecursively(path);

        if (string.IsNullOrWhiteSpace(kitStorePath))
        {
            throw new KitRootPathNotFoundException(path);
        }

        return new KitStore(kitStorePath);
    }

    private static string FindKitRootPathRecursively(string startPath)
    {
        const int maxRecursionDepth = 20;
        
        var recursionDepth = 0;
        
        while (recursionDepth++ < maxRecursionDepth)
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                return string.Empty;
            }

            if (Directory.Exists(Path.Combine(startPath, KitStorePaths.RootPath)))
            {
                return startPath;
            }

            var parentDirectory = Directory.GetParent(startPath);

            if (parentDirectory == null) return string.Empty;
            startPath = parentDirectory.FullName;
        }
        
        return string.Empty;
    }
}