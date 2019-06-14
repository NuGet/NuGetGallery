// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System.IO;
using System;

namespace NuGet.Indexing
{
    public class FileLoader : ILoader
    {
        string _folder;

        public FileLoader()
        {
            _folder = null;
        }

        public FileLoader(string folder)
        {
            _folder = folder.Trim('\\') + '\\';
        }

        public JsonReader GetReader(string fileName)
        {
            string fullName = _folder == null ? fileName : Path.Combine(_folder, fileName);
            return new JsonTextReader(new StreamReader(fullName));
        }

        public DateTime? GetLastUpdateTime(string fileName)
        {
            string fullName = _folder == null ? fileName : Path.Combine(_folder, fileName);
            return File.Exists(fullName)
                ? File.GetLastWriteTimeUtc(fullName)
                : (DateTime?)null;
        }

        public bool Reload(IndexingConfiguration config)
        {
            // no-op because local files do not need to be reloaded
            return false;
        }
    }
}
