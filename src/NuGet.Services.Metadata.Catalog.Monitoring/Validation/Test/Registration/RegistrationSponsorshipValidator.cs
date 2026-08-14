// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Exceptions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Registration
{
    /// <summary>
    /// Validates that the registration-scoped (ID-level) sponsorship URLs in V3 match the database. Sponsorship URLs
    /// are stored on the registration index root and are the same for every version of a package ID.
    /// </summary>
    public class RegistrationSponsorshipValidator : RegistrationIndexValidator
    {
        public RegistrationSponsorshipValidator(
            RegistrationEndpoint endpoint,
            ValidatorConfiguration config,
            ILogger<RegistrationSponsorshipValidator> logger)
            : base(endpoint, config, logger)
        {
        }

        public override Task CompareIndexAsync(ValidationContext context, PackageRegistrationIndexMetadata database, PackageRegistrationIndexMetadata v3)
        {
            var databaseUrls = Normalize(database.SponsorshipUrls);
            var v3Urls = Normalize(v3.SponsorshipUrls);

            // Sponsorship URLs are an unordered set; compare without regard to ordering or duplicates.
            if (!databaseUrls.SetEquals(v3Urls))
            {
                throw new MetadataFieldInconsistencyException<PackageRegistrationIndexMetadata>(
                    database, v3,
                    nameof(PackageRegistrationIndexMetadata.SponsorshipUrls),
                    i => i.SponsorshipUrls == null ? null : string.Join(", ", i.SponsorshipUrls));
            }

            return Task.CompletedTask;
        }

        private static HashSet<string> Normalize(IEnumerable<string> urls)
        {
            return urls == null
                ? new HashSet<string>()
                : new HashSet<string>(urls);
        }
    }
}
