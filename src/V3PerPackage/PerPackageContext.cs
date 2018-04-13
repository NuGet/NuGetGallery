// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.V3PerPackage
{
    /// <summary>
    /// A child context of <see cref="PerBatchContext"/>. This is the most granular unit of work and is corresponding
    /// to a single package. This is contains information used by <see cref="PerBatchProcessor"/>.
    /// </summary>
    public class PerPackageContext
    {
        public PerPackageContext(PerBatchContext batch, string packageId, string packageVersion)
        {
            Batch = batch;
            PackageId = packageId;
            PackageVersion = packageVersion;
        }

        public GlobalContext Global => Batch.Global;
        public PerProcessContext Process => Batch.Process;
        public PerWorkerContext Worker => Batch.Worker;
        public PerBatchContext Batch { get; }

        public string PackageId { get; set; }

        /// <summary>
        /// The normalized package version.
        /// </summary>
        public string PackageVersion { get; set; }

        public Uri PackageUri => new Uri($"{Global.ContentBaseAddress}{PackageId.ToLowerInvariant()}.{PackageVersion.ToLowerInvariant()}.nupkg");
    }
}
