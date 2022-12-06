using EasyForNet.Application.Dependencies;

namespace EasyForNet.Key;

public class KeyManager : IKeyManager, ITransientDependency
{
    public string GlobalKey(string key)
    {
        return !key.StartsWith("Global_") ? $"Global_{key}" : key;
    }
}