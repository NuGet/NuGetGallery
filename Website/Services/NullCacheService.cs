using System;

namespace NuGetGallery
{
    /// <summary>
    /// A cache service that does not store objects at all.
    /// </summary>
    public sealed class NullCacheService : ICacheService
    {
        public byte[] GetItem(string key)
        {
            return null;
        }

        public void SetItem(string key, byte[] item)
        {
        }
    }
}