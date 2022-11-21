using EasyForNet.Application.Dependencies;

namespace EasyForNet.Key;

public interface IKeyManager
{
    string GlobalKey(string key);
}

public class KeyManager : IKeyManager, ITransientDependency
{
    public string GlobalKey(string key)
    {
        return !key.StartsWith("Global_") ? $"Global_{key}" : key;
    }
}