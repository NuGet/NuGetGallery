// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Storage
{
    public class FileStorageFactory : StorageFactory
    {
        private readonly string _path;
        private readonly ILogger<FileStorage> _fileStorageLogger;

        public FileStorageFactory(Uri baseAddress, string path, ILogger<FileStorage> fileStorageLogger)
        {
            BaseAddress = new Uri(baseAddress.ToString().TrimEnd('/') + '/');
            _path = path.TrimEnd('\\') + '\\';
            _fileStorageLogger = fileStorageLogger;
        }

        public override Storage Create(string name = null)
        {
            string fileSystemPath = (name == null) ? _path.Trim('\\') : _path + name;
            string uriPath = name ?? string.Empty;

            return new FileStorage(BaseAddress + uriPath, fileSystemPath, _fileStorageLogger) { Verbose = Verbose };
        }
    }
}
