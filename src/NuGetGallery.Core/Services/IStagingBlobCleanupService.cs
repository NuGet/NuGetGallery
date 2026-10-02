// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        /// Determines whether a retained current attempt still needs a requested file.
        /// </summary>
        bool HasLiveReference(StagingBlobCleanup cleanup);
    }
}
