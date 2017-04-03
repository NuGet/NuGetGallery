// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng
{
    /// <summary>
    /// Contains the storage factories used to create registration blobs.
    /// </summary>
    public class RegistrationStorageFactories
    {
        public RegistrationStorageFactories(StorageFactory legacyStorageFactory, StorageFactory semVer2StorageFactory)
        {
            if (legacyStorageFactory == null)
            {
                throw new ArgumentNullException(nameof(legacyStorageFactory));
            }

            LegacyStorageFactory = legacyStorageFactory;
            SemVer2StorageFactory = semVer2StorageFactory;
        }

        /// <summary>
        /// The factory used for registration hives that do not have SemVer 2.0.0 packages. This factory is typically
        /// an <see cref="AggregateStorageFactory"/> that writes both gzipped and non-gzipped blobs.
        /// </summary>
        public StorageFactory LegacyStorageFactory { get; }

        /// <summary>
        /// The factory used for registration hives that have all packages (SemVer 2.0.0 and otherwise).
        /// </summary>
        public StorageFactory SemVer2StorageFactory { get; }
    }
}
