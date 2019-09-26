// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class RegistrationRequireLicenseAcceptanceValidator : RegistrationIndexValidator
    {
        public RegistrationRequireLicenseAcceptanceValidator(
            RegistrationEndpoint endpoint,
            ValidatorConfiguration config,
            ILogger<RegistrationRequireLicenseAcceptanceValidator> logger)
            : base(endpoint, config, logger)
        {
        }

        public override Task CompareIndexAsync(ValidationContext context, PackageRegistrationIndexMetadata database, PackageRegistrationIndexMetadata v3)
        {
            var isEqual = database.RequireLicenseAcceptance == v3.RequireLicenseAcceptance;

            if (!isEqual)
            {
                throw new MetadataFieldInconsistencyException<PackageRegistrationIndexMetadata>(
                    database, v3,
                    nameof(PackageRegistrationIndexMetadata.RequireLicenseAcceptance),
                    m => m.RequireLicenseAcceptance);
            }

            return Task.FromResult(0);
        }
    }
}