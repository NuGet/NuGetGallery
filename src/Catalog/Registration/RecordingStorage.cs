using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RecordingStorage : IStorage
    {
        IStorage _innerStorage;

        public RecordingStorage(IStorage storage)
        {
            _innerStorage = storage;

            Loaded = new HashSet<Uri>();
            Saved = new HashSet<Uri>();
        }

        public HashSet<Uri> Loaded { get; private set; }
        public HashSet<Uri> Saved { get; private set; }

        public Task Save(Uri resourceUri, StorageContent content)
        {
            Task result = _innerStorage.Save(resourceUri, content);
            Saved.Add(resourceUri);
            return result;
        }

        public Task<StorageContent> Load(Uri resourceUri)
        {
            Task<StorageContent> result = _innerStorage.Load(resourceUri);
            Loaded.Add(resourceUri);
            return result;
        }

        public Task Delete(Uri resourceUri)
        {
            return _innerStorage.Delete(resourceUri);
        }

        public Task<string> LoadString(Uri resourceUri)
        {
            Task<string> result = _innerStorage.LoadString(resourceUri);
            Loaded.Add(resourceUri);
            return result;
        }

        public Uri BaseAddress
        {
            get { return _innerStorage.BaseAddress; }
        }

        public Uri ResolveUri(string relativeUri)
        {
            return _innerStorage.ResolveUri(relativeUri);
        }
    }
}
