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
        /// The normalized version of the package that was deleted.
        /// </summary>
        public string NormalizedVersion { get; private set; }

        /// <summary>
        /// The timestamp that the package was deleted.
        /// </summary>
        /// <remarks>
        /// This is calculated by the instance that performs the delete.
        /// As a result, there is no guarantee that the timestamps of multiple deletes are in order.
        /// In other words, it is possible that a package deleted at time X and a package deleted at time X + 1 have timestamps of Y + 1 and Y respectively.
        /// </remarks>
        public DateTime DeletedTimestamp { get; private set; }

        [JsonConstructor]
        public PackageDeletionRecord(string id, string normalizedVersion, DateTime deletedTimestamp)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(nameof(id));
            }

            if (string.IsNullOrEmpty(normalizedVersion))
            {
                throw new ArgumentException(nameof(normalizedVersion));
            }

            Id = id;
            NormalizedVersion = normalizedVersion;
            DeletedTimestamp = deletedTimestamp;
        }

        public PackageDeletionRecord(Package package)
            : this(package?.PackageRegistration?.Id, package?.NormalizedVersion, DateTime.UtcNow)
        {
        }
    }
}