
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

        public override Storage Create(string name)
        {
            return new FileStorage(BaseAddress + name, _path + name);
        }
    }
}
