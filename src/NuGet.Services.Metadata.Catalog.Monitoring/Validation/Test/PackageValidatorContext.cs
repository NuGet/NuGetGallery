// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// The data to be passed to <see cref="PackageValidator.ValidateAsync(PackageValidatorContext, CollectorHttpClient, System.Threading.CancellationToken)"/>.
    /// </summary>
    public class PackageValidatorContext
    {
        /// <summary>
        /// This should be incremented every time the structure of this class changes.
        /// </summary>
        public const int Version = 1;

        /// <summary>
        /// The package to run validations on.
        /// </summary>
        public FeedPackageIdentity Package { get; }

        /// <summary>
        /// The catalog entries that initiated this request to run validations.
        /// </summary>
        /// <remarks>
        /// If null, the latest catalog index entry for the package will be validated against.
        /// </remarks>
        public IEnumerable<CatalogIndexEntry> CatalogEntries { get; }

        [JsonConstructor]
        public PackageValidatorContext(FeedPackageIdentity package, IEnumerable<CatalogIndexEntry> catalogEntries)
        {
            Package = package ?? throw new ArgumentNullException(nameof(package));
            CatalogEntries = catalogEntries;
        }

        public PackageValidatorContext(PackageMonitoringStatus status)
            : this(status.Package, status.ValidationResult?.CatalogEntries)
        {
        }
    }
}