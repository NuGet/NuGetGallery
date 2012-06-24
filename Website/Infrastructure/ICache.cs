using System;

namespace NuGetGallery
{
    public interface ICache
    {
        T Get<T>(string key);
        void Remove(string key);
        void Set(string key, object value);
    }
}
