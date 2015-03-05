using System;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public interface IStorage
    {
        Task Save(Uri resourceUri, StorageContent content);
        Task<StorageContent> Load(Uri resourceUri);
        Task Delete(Uri resourceUri);
        Task<string> LoadString(Uri resourceUri);
        Uri BaseAddress { get; }
        Uri ResolveUri(string relativeUri);
    }
}
