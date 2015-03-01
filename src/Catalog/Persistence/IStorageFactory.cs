using System;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface IStorageFactory
    {
        Storage Create(string name = null);
        Uri BaseAddress { get; }
    }
}
