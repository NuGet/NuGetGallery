using System.IO;

namespace NuGetGallery
{
    public interface IFileSystemService
    {
        void CreateDirectory(string path);
        bool DirectoryExists(string path);
        Stream OpenWrite(string path);
    }
}