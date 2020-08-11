// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageValidationResult
    {
        [JsonProperty("package")]
        public PackageIdentity Package { get; }

        [JsonProperty("catalogEntries")]
        public IEnumerable<CatalogIndexEntry> CatalogEntries { get; }

        [JsonProperty("deletionAuditEntries")]
        public IEnumerable<DeletionAuditEntry> DeletionAuditEntries { get; }

        [JsonProperty("results")]
        public IEnumerable<AggregateValidationResult> AggregateValidationResults { get; }

        public PackageValidationResult(ValidationContext context, IEnumerable<AggregateValidationResult> results)
            : this(context.Package, context.Entries, context.DeletionAuditEntries, results)
        {
        }

        [JsonConstructor]
        public PackageValidationResult(
            PackageIdentity package, 
            IEnumerable<CatalogIndexEntry> catalogEntries, 
            IEnumerable<DeletionAuditEntry> deletionAuditEntries, 
            IEnumerable<AggregateValidationResult> results)
        {
            Package = package;
            CatalogEntries = catalogEntries;
            DeletionAuditEntries = deletionAuditEntries;
            AggregateValidationResults = results;
        }
    }
}
