namespace CreativeCoders.ShadeKit.Core.Store;

public interface IKitStoreFactory
{
    IKitStore Create();

    IKitStore Create(string path);
}