// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Metadata.Catalog.Persistence
{
    public class FileStorageFactory : StorageFactory
    {
        private readonly string _path;

        public FileStorageFactory(Uri baseAddress, string path, bool verbose)
        {
            BaseAddress = new Uri(baseAddress.ToString().TrimEnd('/') + '/');
            _path = path.TrimEnd('\\') + '\\';

            Verbose = verbose;
        }

        public override Storage Create(string name = null)
        {
            string fileSystemPath = (name == null) ? _path.Trim('\\') : _path + name;
            string uriPath = name ?? string.Empty;

            return new FileStorage(BaseAddress + uriPath, fileSystemPath, Verbose);
        }
    }
}