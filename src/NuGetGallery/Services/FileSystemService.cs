﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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

        public Stream OpenWrite(string path, bool overwrite)
        {
            return new FileStream(
                path,
                overwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
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

        public virtual void Copy(string sourceFileName, string destFileName, bool overwrite)
        {
            File.Copy(sourceFileName, destFileName, overwrite);
        }
    }
}