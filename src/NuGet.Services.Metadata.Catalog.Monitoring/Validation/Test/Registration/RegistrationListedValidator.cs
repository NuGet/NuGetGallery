// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class RegistrationListedValidator : RegistrationLeafValidator
    {
        public RegistrationListedValidator(
            ValidatorConfiguration config,
            ILogger<RegistrationListedValidator> logger)
            : base(config, logger)
        {
        }

        public override Task<bool> ShouldRunLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata v2,
            PackageRegistrationLeafMetadata v3)
        {
            return Task.FromResult(v2 != null && v3 != null);
        }

        public override Task CompareLeafAsync(
            ValidationContext context,
            PackageRegistrationLeafMetadata v2,
            PackageRegistrationLeafMetadata v3)
        {
            if (v2.Listed != v3.Listed)
            {
                throw new MetadataFieldInconsistencyException<PackageRegistrationLeafMetadata>(
                    v2, v3,
                    nameof(PackageRegistrationLeafMetadata.Listed),
                    m => m.Listed);
            }

            return Task.FromResult(0);
        }
    }
}