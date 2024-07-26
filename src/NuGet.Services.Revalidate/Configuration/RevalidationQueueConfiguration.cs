// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Revalidate
{
    public class RevalidationQueueConfiguration
    {
        /// <summary>
        /// The maximum number of revalidations that should be returned by <see cref="IRevalidationQueue.NextAsync"/>.
        /// </summary>
        public int MaxBatchSize { get; set; } = 64;

        /// <summary>
        /// If non-null, this skips revalidations of packages with more than this many versions.
        /// </summary>
        public int? MaximumPackageVersions { get; set; }
    }
}
