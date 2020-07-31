// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class RegistrationListedValidator : RegistrationLeafValidator
    {
        public RegistrationListedValidator(
            RegistrationEndpoint endpoint,
            ValidatorConfiguration config,
            ILogger<RegistrationListedValidator> logger)
            : base(endpoint, config, logger)
        {
        }

        public override Task<ShouldRunTestResult> ShouldRunLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata database,
            PackageRegistrationLeafMetadata v3)
        {
            return Task.FromResult(database != null && v3 != null ? ShouldRunTestResult.Yes : ShouldRunTestResult.No);
        }

        public override Task CompareLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata database,
            PackageRegistrationLeafMetadata v3)
        {
            if (database.Listed != v3.Listed)
            {
                throw new MetadataFieldInconsistencyException<PackageRegistrationLeafMetadata>(
                    database, v3,
                    nameof(PackageRegistrationLeafMetadata.Listed),
                    m => m.Listed);
            }

            return Task.FromResult(0);
        }
    }
}