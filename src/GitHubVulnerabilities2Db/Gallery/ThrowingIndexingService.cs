// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery;

namespace GitHubVulnerabilities2Db.Gallery
{
    /// <remarks>
    /// The changes to the database that need indexing will be handled by our V3 pipeline.
    /// </remarks>
    public class ThrowingIndexingService : IIndexingService
    {
        public string IndexPath => throw new NotImplementedException();

        public bool IsLocal => throw new NotImplementedException();

        public Task<int> GetDocumentCount()
        {
            throw new NotImplementedException();
        }

        public Task<long> GetIndexSizeInBytes()
        {
            throw new NotImplementedException();
        }

        public Task<DateTime?> GetLastWriteTime()
        {
            throw new NotImplementedException();
        }

        public void UpdateIndex()
        {
            throw new NotImplementedException();
        }

        public void UpdateIndex(bool forceRefresh)
        {
            throw new NotImplementedException();
        }

        /// <remarks>
        /// We expect this to be called by <see cref="PackageUpdateService"/>.
        /// </remarks>
        public void UpdatePackage(Package package)
        {
        }
    }
}