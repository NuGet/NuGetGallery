
using System;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class FileStorageFactory : StorageFactory
    {
        string _path;
        
        public FileStorageFactory(Uri baseAddress, string path)
        {
            BaseAddress = new Uri(baseAddress.ToString().TrimEnd('/') + '/');
            _path = path.TrimEnd('\\') + '\\';
        }

        public override Storage Create(string name = null)
        {
            string fileSystemPath = (name == null) ? _path.Trim('\\') : _path + name;
            string uriPath = name ?? string.Empty;

            return new FileStorage(BaseAddress + uriPath, fileSystemPath) { Verbose = Verbose };
        }
    }
}
