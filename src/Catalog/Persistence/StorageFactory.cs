using System;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class StorageFactory : IStorageFactory
    {
        public abstract Storage Create(string name = null);

        public Uri BaseAddress { get; protected set; }

        public bool Verbose { get; set; }

        public override string ToString()
        {
            return BaseAddress.ToString();
        }
    }
}
