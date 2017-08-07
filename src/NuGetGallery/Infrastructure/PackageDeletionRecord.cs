// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace NuGetGallery
{
    /// <summary>
    /// Stores information about a <see cref="Package"/> that was deleted.
    /// </summary>
    public class PackageDeletionRecord
    {
        /// <summary>
        /// The id of the package that was deleted.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// The version of the package that was deleted.
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// The timestamp that the package was deleted.
        /// </summary>
        /// <remarks>
        /// This is calculated by the instance that performs the delete.
        /// As a result, there is no guarantee that the timestamps of multiple deletes are in order.
        /// In other words, it is possible that a package deleted at time X and a package deleted at time X + 1 have timestamps of Y + 1 and Y respectively.
        /// </remarks>
        public DateTimeOffset DeletedTimestamp { get; private set; }

        [JsonConstructor]
        public PackageDeletionRecord(string id, string version, DateTimeOffset deletedTimestamp)
        {
            Id = id;
            Version = version;
            DeletedTimestamp = deletedTimestamp;
        }

        public PackageDeletionRecord(Package package)
            : this(package.PackageRegistration.Id, package.Version, DateTimeOffset.UtcNow)
        {
        }
    }
}