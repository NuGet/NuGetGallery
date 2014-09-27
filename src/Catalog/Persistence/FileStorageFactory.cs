
namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class FileStorageFactory : StorageFactory
    {
        string _baseAddress;
        string _path;
        
        public FileStorageFactory(string baseAddress, string path)
        {
            _baseAddress = baseAddress.TrimEnd('/') + '/';
            _path = path.TrimEnd('\\') + '\\';
        }

        public override Storage Create(string name)
        {
            return new FileStorage(_baseAddress + name, _path + name);
        }
    }
}
