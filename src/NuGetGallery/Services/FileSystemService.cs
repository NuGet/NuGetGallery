using System;
using System.IO;

namespace NuGetGallery
{
    public class FileSystemService : IFileSystemService
    {
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public void DeleteFile(string path)
        {
            File.Delete(path);
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public Stream OpenRead(string path)
        {
            return File.OpenRead(path);
        }

        public Stream OpenWrite(string path)
        {
            return File.OpenWrite(path);
        }

        public DateTimeOffset GetCreationTimeUtc(string path)
        {
            return File.GetCreationTimeUtc(path);
        }

        public IFileReference GetFileReference(string path)
        {
            var info = new FileInfo(path);
            return info.Exists ? new LocalFileReference(info) : null;
        }
    }
}