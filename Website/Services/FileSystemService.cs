using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;

namespace NuGetGallery {
    public class FileSystemService : IFileSystemService {
        public void CreateDirectory(string path) {
            Directory.CreateDirectory(path);
        }

        public bool DirectoryExists(string path) {
            return Directory.Exists(path);
        }

        public System.IO.FileStream OpenWrite(string path) {
            return File.OpenWrite(path);
        }
    }
}