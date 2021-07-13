// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery.AccountDeleter
{
    /// <summary>
    /// This class must exist becuase <see cref="PackageUpdateService"/> requires an <see cref="IIndexingService"/>
    /// However, since our indexing depends directly on DB, we need to catch these calls and do nothing.
    /// We have only no-oped the calls that we expect to need. Any unexpected call should throw here, and the need for a real indexing service should be re-evaluated if this occurs.
    /// </summary>
    public class EmptyIndexingService : IIndexingService
    {
        public EmptyIndexingService()
        {

        }

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
            // Do nothing. We have no index to update here.
        }

        public void UpdateIndex(bool forceRefresh)
        {
            UpdateIndex();
        }

        public void UpdatePackage(Package package)
        {
            UpdateIndex();
        }
    }
}
