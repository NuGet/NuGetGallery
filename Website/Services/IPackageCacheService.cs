using System;

namespace NuGetGallery
{
    public interface IPackageCacheService
    {
        byte[] GetBytes(string key);
        void SetBytes(string key, byte[] data);
    }
}