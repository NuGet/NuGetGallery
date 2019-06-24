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

        /// <remarks>
        /// In the past, the catalog entries referred to the CDN's host instead our DNS.
        /// If we encounter one of these outdated hosts, we should hit the proper host instead.
        /// </remarks>
        private static readonly IDictionary<string, string> _catalogEntryUriHostMap =
            new Dictionary<string, string>
            {
                { "az635243.vo.msecnd.net", "apidev.nugettest.org" },
                { "az636225.vo.msecnd.net", "apiint.nugettest.org" }
            };

        public static async Task<PackageTimestampMetadata> FromCatalogEntry(
            CollectorHttpClient client,
            CatalogIndexEntry catalogEntry)
        {
            var uri = catalogEntry.Uri;
            if (_catalogEntryUriHostMap.TryGetValue(catalogEntry.Uri.Host, out var replacementHost))
            {
                var builder = new UriBuilder(uri)
                {
                    Host = replacementHost
                };

                uri = builder.Uri;
            }

            var catalogLeaf = await client.GetJObjectAsync(uri);

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

            return packageTimestampMetadatas
                .Where(p => p != null)
                .OrderByDescending(p => p.Last)
                .First();
        }
    }
}