// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Queues obsolete staging files within the caller's transaction and checks retained file ownership.
    /// </summary>
    public interface IStagingBlobCleanupService
    {
        /// <summary>
        /// Queues uploaded and validated package files, including earlier attempts of the identity.
        /// </summary>
        void QueuePackageFiles(int stagedPackageIdentityKey);

        /// <summary>
        /// Queues uploaded symbol files, including earlier attempts of the identity.
        /// </summary>
        void QueueSymbolFiles(int stagedPackageIdentityKey);

        /// <summary>
        /// Gets requested file paths still needed by retained current attempts using page-scoped queries.
        /// </summary>
        Task<HashSet<string>> GetLiveReferencedPathsAsync(IReadOnlyCollection<StagingBlobCleanup> cleanups);
    }
}
