// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    public class HijackDocumentChanges
    {
        public HijackDocumentChanges(
            bool delete,
            bool updateMetadata,
            bool latestStableSemVer1,
            bool latestSemVer1,
            bool latestStableSemVer2,
            bool latestSemVer2)
        {
            if (delete && updateMetadata)
            {
                throw new ArgumentException("Deleting and updating a hijack document are mutually exclusive.");
            }

            if (delete && (latestStableSemVer1 || latestSemVer1 || latestStableSemVer2 || latestSemVer2))
            {
                throw new ArgumentException("Deleting a document is mutually exclusive with making that document that latest.");
            }

            Delete = delete;
            UpdateMetadata = updateMetadata;
            LatestStableSemVer1 = latestStableSemVer1;
            LatestSemVer1 = latestSemVer1;
            LatestStableSemVer2 = latestStableSemVer2;
            LatestSemVer2 = latestSemVer2;
        }

        /// <summary>
        /// Whether or not to delete the document. If this value is true, all other properties (aside from
        /// <see cref="Version"/>) are ignored.
        /// </summary>
        public bool Delete { get; }

        /// <summary>
        /// Whether or not to update the metadata of this version.
        /// </summary>
        public bool UpdateMetadata { get; }
        
        /// <summary>
        /// Whether or not this version is the latest version, excluding prerelease versions and SemVer 2.0.0 versions.
        /// This is associated with <see cref="SearchFilters.Default"/>.
        /// </summary>
        public bool LatestStableSemVer1 { get; }

        /// <summary>
        /// Whether or not this version is the latest version, excluding SemVer 2.0.0 versions. This is associated with
        /// <see cref="SearchFilters.IncludePrerelease"/>.
        /// </summary>
        public bool LatestSemVer1 { get; }

        /// <summary>
        /// Whether or not this version is the latest version, excluding prerelease versions. This is associated with
        /// <see cref="SearchFilters.IncludeSemVer2"/>.
        /// </summary>
        public bool LatestStableSemVer2 { get; }

        /// <summary>
        /// Whether or not this version is the latest version. This is associated with
        /// <see cref="SearchFilters.Default"/>.
        /// </summary>
        public bool LatestSemVer2 { get; }
    }
}
