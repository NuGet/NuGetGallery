// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace NuGetGallery
{
    public interface IFileSystemService
    {
        void CreateDirectory(string path);
        void DeleteFile(string path);
        bool DirectoryExists(string path);
        bool FileExists(string path);
        Stream OpenRead(string path);
        Stream OpenWrite(string path, bool overwrite);
        DateTimeOffset GetCreationTimeUtc(string path);
        IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption);

        IFileReference GetFileReference(string path);

        void Copy(string sourceFileName, string destFileName, bool overwrite);
    }
}
