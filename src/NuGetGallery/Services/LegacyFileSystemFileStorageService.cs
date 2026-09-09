// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class LegacyFileSystemFileStorageService : FileSystemFileStorageService
    {
        private readonly IAppConfiguration _configuration;

        public LegacyFileSystemFileStorageService(
            IAppConfiguration configuration,
            IFileSystemService fileSystemService)
            : base(FileStoragePathResolver.Resolve(configuration.FileStorageDirectory), fileSystemService)
        {
            _configuration = configuration;
        }

        public override Task<bool> IsAvailableAsync()
        {
            return Task.FromResult(Directory.Exists(_configuration.FileStorageDirectory));
        }
    }
}
