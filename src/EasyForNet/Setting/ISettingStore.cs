using System.Collections.Generic;
using System.Threading.Tasks;

namespace EasyForNet.Setting
{
    public interface ISettingStore
    {
        TValue Get<TValue>(string key);
        Task<TValue> GetAsync<TValue>(string key);
        void Set<TValue>(string key, TValue value);
        Task SetAsync<TValue>(string key, TValue value);
        Dictionary<string, string> GetAll();
        Task<Dictionary<string, string>> GetAllAsync();
    }
}
