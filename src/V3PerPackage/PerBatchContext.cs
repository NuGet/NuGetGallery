// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.V3PerPackage
{
    /// <summary>
    /// A child context of <see cref="PerWorkerContext"/> and a parent context of <see cref="PerPackageContext"/>.
    /// This is contains information used by <see cref="PerBatchProcessor"/>.
    /// </summary>
    public class PerBatchContext
    {
        public PerBatchContext(PerWorkerContext workerContext, string name)
        {
            Name = name;
            Worker = workerContext;
        }

        public GlobalContext Global => Worker.Global;
        public PerProcessContext Process => Worker.Process;

        public string Name { get; }

        public PerWorkerContext Worker { get; }

        /// <summary>
        /// catalog2lucene does not support subdirectories of the container so the container name needs to be
        /// batch-specific. In this case we choose it to be package specific to avoid Delete/Create HTTP 409 errors
        /// that can be thrown by Azure Blob Storage.
        /// </summary>
        public string LuceneContainerName => $"v3-lucene-{Name}";
    }
}
