// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.V3PerPackage
{
    /// <summary>
    /// A child context of <see cref="GlobalContext"/> and a parent context of <see cref="PerWorkerContext"/>.
    /// This is contains information used by <see cref="PerProcessProcessor"/>. There should be a single instance of
    /// this type per process.
    /// </summary>
    public class PerProcessContext
    {
        public PerProcessContext(GlobalContext global, string name, int workerCount, int messageCount, int batchSize)
        {
            Global = global;
            Name = name;
            WorkerCount = workerCount;
            MessageCount = messageCount;
            BatchSize = batchSize;
        }

        public GlobalContext Global { get; }
        public string Name { get; }

        public int WorkerCount { get; }
        public int MessageCount { get; }
        public int BatchSize { get; }

        /// <summary>
        /// The flat container URL is used in a static (<see cref="RegistrationMakerCatalogItem.PackagePathProvider"/>)
        /// so it must be process-specific. This means that catalog2dnx work across multiple workers must serialize
        /// with respect to package ID.
        /// </summary>
        public string FlatContainerStoragePath => Name;
    }
}
