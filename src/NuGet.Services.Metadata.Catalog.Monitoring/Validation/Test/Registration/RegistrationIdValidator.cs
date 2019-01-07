// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class RegistrationIdValidator : RegistrationIndexValidator
    {
        public RegistrationIdValidator(ValidatorConfiguration config, ILogger<RegistrationIdValidator> logger)
            : base(config, logger)
        {
        }

        public override Task CompareIndexAsync(
            ValidationContext context,
            PackageRegistrationIndexMetadata v2,
            PackageRegistrationIndexMetadata v3)
        {
            if (!v2.Id.Equals(v3.Id, System.StringComparison.OrdinalIgnoreCase))
            {
                throw new MetadataFieldInconsistencyException<PackageRegistrationIndexMetadata>(
                    v2, v3,
                    nameof(PackageRegistrationIndexMetadata.Id),
                    m => m.Id);
            }

            return Task.FromResult(0);
        }
    }
}