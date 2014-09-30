
namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class FileStorageFactory : StorageFactory
    {
        string _path;
        
        public FileStorageFactory(string baseAddress, string path)
        {
            BaseAddress = baseAddress.TrimEnd('/') + '/';
            _path = path.TrimEnd('\\') + '\\';
        }

        public override Storage Create(string name)
        {
            return new FileStorage(BaseAddress + name, _path + name);
        }
    }
}
