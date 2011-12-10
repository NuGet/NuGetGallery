﻿using System.IO;

namespace NuGetGallery
{
    public interface IFileSystemService
    {
        void CreateDirectory(string path);
        void DeleteFile(string path);
        bool DirectoryExists(string path);
        bool FileExists(string path);
        Stream OpenRead(string path);
        Stream OpenWrite(string path);
    }
}