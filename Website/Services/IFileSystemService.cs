using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;

namespace NuGetGallery {
    public interface IFileSystemService {
        void CreateDirectory(string path);
        bool DirectoryExists(string path);
        FileStream OpenWrite(string path);
    }
}