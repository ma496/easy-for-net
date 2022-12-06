﻿using System.Threading.Tasks;

namespace EasyForNet.Setting;

public interface ISettingManager
{
    TValue Get<TValue>(string key);
    Task<TValue> GetAsync<TValue>(string key);
    void Set<TValue>(string key, TValue value);
    Task SetAsync<TValue>(string key, TValue value);
}