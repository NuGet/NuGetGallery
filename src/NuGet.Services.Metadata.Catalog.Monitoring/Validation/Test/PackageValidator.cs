// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Stores a set of <see cref="AggregateValidator"/>s and runs them.
    /// </summary>
    public class PackageValidator
    {
        private readonly IEnumerable<IAggregateValidator> _endpointValidators;
        private readonly ILogger<PackageValidator> _logger;

        private readonly StorageFactory _auditingStorageFactory;

        public PackageValidator(
            IEnumerable<IAggregateValidator> endpointValidators,
            StorageFactory auditingStorageFactory,
            ILogger<PackageValidator> logger)
        {
            if (endpointValidators.Count() < 1)
            {
                throw new ArgumentException("Must supply at least one endpoint!", nameof(endpointValidators));
            }

            _endpointValidators = endpointValidators.ToList();
            _auditingStorageFactory = auditingStorageFactory ?? throw new ArgumentNullException(nameof(auditingStorageFactory));
            _logger = logger;
        }

        /// <summary>
        /// Runs <see cref="IValidator"/>s from the <see cref="IAggregateValidator"/>s against a package.
        /// </summary>
        /// <returns>A <see cref="PackageValidationResult"/> generated from the results of the <see cref="IValidator"/>s.</returns>
        public async Task<PackageValidationResult> ValidateAsync(string packageId, string packageVersion, IList<JObject> catalogEntriesJson, CollectorHttpClient client, CancellationToken cancellationToken)
        {
            var package = new PackageIdentity(packageId, NuGetVersion.Parse(packageVersion));
            var catalogEntries = catalogEntriesJson.Select(c => new CatalogIndexEntry(c));
            var deletionAuditEntries = await DeletionAuditEntry.GetAsync(
                _auditingStorageFactory, 
                cancellationToken, 
                package, 
                logger: _logger);

            var validationContext = new ValidationContext(package, catalogEntries, deletionAuditEntries, client, cancellationToken);
            var results = await Task.WhenAll(_endpointValidators.Select(endpoint => endpoint.ValidateAsync(validationContext)));
            return new PackageValidationResult(validationContext, results);
        }
    }
}
