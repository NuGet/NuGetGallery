// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Represents the timestamp metadata for a single package in a package source.
    /// </summary>
    public class PackageTimestampMetadata : INuGetResource
    {
        public bool Exists { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? LastEdited { get; set; }
        public DateTime? Deleted { get; set; }

        /// <summary>
        /// The most recent time the package was created, edited, or deleted in a package source.
        /// </summary>
        public DateTime? Last => new[] { Created, LastEdited, Deleted }.Max();

        /// <summary>
        /// Creates a <see cref="PackageTimestampMetadata"/> that represents a package that exists on the package source.
        /// </summary>
        public static PackageTimestampMetadata CreateForExistingPackage(DateTime created, DateTime lastEdited)
        {
            return new PackageTimestampMetadata
            {
                Exists = true,
                Created = created,
                LastEdited = lastEdited,
                Deleted = null
            };
        }

        /// <summary>
        /// Creates a <see cref="PackageTimestampMetadata"/> that represents a package that is missing from the package source.
        /// </summary>
        public static PackageTimestampMetadata CreateForMissingPackage(DateTime? deleted)
        {
            return new PackageTimestampMetadata
            {
                Exists = false,
                Created = null,
                LastEdited = null,
                Deleted = deleted
            };
        }

        public static async Task<PackageTimestampMetadata> FromCatalogEntry(
            CollectorHttpClient client,
            CatalogIndexEntry catalogEntry)
        {
            var catalogLeaf = await client.GetJObjectAsync(catalogEntry.Uri);

            try
            {
                if (catalogEntry.IsDelete)
                {
                    // On the catalog page for a delete, the published value is the timestamp the package was deleted from the audit records.
                    var deleted = catalogLeaf.GetValue("published").Value<DateTimeOffset>();

                    return CreateForMissingPackage(deleted.DateTime);
                }
                else
                {
                    var created = catalogLeaf.GetValue("created").Value<DateTimeOffset>();
                    var lastEdited = catalogLeaf.GetValue("lastEdited").Value<DateTimeOffset>();

                    return CreateForExistingPackage(created.DateTime, lastEdited.DateTime);
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException("Failed to create PackageTimestampMetadata from CatalogIndexEntry!", e);
            }
        }

        public static async Task<PackageTimestampMetadata> FromCatalogEntries(
            CollectorHttpClient client,
            IEnumerable<CatalogIndexEntry> catalogEntries)
        {
            var packageTimestampMetadatas =
                await Task.WhenAll(catalogEntries.Select(entry => FromCatalogEntry(client, entry)));
            var maxTimestamp = packageTimestampMetadatas.Where(p => p != null).Max(p => p.Last);
            return packageTimestampMetadatas.FirstOrDefault(p => p.Last == maxTimestamp);
        }
    }
}