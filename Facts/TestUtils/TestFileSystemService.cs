using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NuGetGallery.TestUtils
{
    public class TestFileSystemService : IFileSystemService, IDisposable
    {
        private readonly HashSet<string> _createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TestFile> _files = new Dictionary<string, TestFile>(StringComparer.OrdinalIgnoreCase);

        public ISet<string> CreatedDirectories { get { return _createdDirectories; } }
        public IDictionary<string, TestFile> Files { get { return _files; } }

        public void CreateDirectory(string path)
        {
            CreatedDirectories.Add(path);
        }

        public void DeleteFile(string path)
        {
            _files.Remove(path);
        }

        public bool DirectoryExists(string path)
        {
            return _createdDirectories.Contains(path);
        }

        public bool FileExists(string path)
        {
            return _files.ContainsKey(path);
        }

        public Stream OpenRead(string path)
        {
            return _files[path].Open(); // Throwing is OK
        }

        public Stream OpenWrite(string path)
        {
            TestFile file;
            if (!_files.TryGetValue(path, out file))
            {
                file = new TestFile();
                _files[path] = file;
            }
            return file.Open();
        }

        public DateTimeOffset GetCreationTimeUtc(string path)
        {
            throw new NotSupportedException("Not currently supported in TestFileSystemService");
        }

        public void Dispose()
        {
            foreach (var file in Files.Values)
            {
                file.Dispose();
            }
        }
    }
}
