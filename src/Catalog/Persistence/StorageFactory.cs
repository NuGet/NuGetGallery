
using System;
namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public abstract class StorageFactory
    {
        public abstract Storage Create(string name);

        public string BaseAddress { get; protected set; }
    }
}
