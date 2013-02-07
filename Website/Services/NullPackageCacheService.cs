using System;

namespace NuGetGallery
{
    /// <summary>
    /// A cache service that does not store objects at all.
    /// </summary>
    public sealed class NullPackageCacheService : IPackageCacheService
    {
        public byte[] GetBytes(string key)
        {
            return null;
        }

        public void SetBytes(string key, byte[] item)
        {
        }
    }
}