using System;

namespace NuGetGallery
{
    public interface ICacheService
    {
        byte[] GetItem(string key);
        void SetItem(string key, byte[] data);
    }
}