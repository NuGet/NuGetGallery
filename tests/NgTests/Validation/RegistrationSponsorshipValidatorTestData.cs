// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Monitoring.Validation.Test.Registration;
using System;
using System.Collections.Generic;

namespace NgTests.Validation
{
    public class RegistrationSponsorshipValidatorTestData : RegistrationIndexValidatorTestData<RegistrationSponsorshipValidator>
    {
        protected override RegistrationSponsorshipValidator CreateValidator(
            ILogger<RegistrationSponsorshipValidator> logger)
        {
            var endpoint = ValidatorTestUtility.CreateRegistrationEndpoint();
            var config = new ValidatorConfiguration("https://nuget.test/packages", requireRepositorySignature: false);

            return new RegistrationSponsorshipValidator(endpoint, config, logger);
        }

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateIndexes
        {
            get
            {
                // Each of these must be distinguishable from every other one as an unordered set of URLs.
                yield return () => new PackageRegistrationIndexMetadata { SponsorshipUrls = null };
                yield return () => new PackageRegistrationIndexMetadata { SponsorshipUrls = new List<string> { "https://github.com/sponsors/a" } };
                yield return () => new PackageRegistrationIndexMetadata { SponsorshipUrls = new List<string> { "https://github.com/sponsors/b" } };
                yield return () => new PackageRegistrationIndexMetadata { SponsorshipUrls = new List<string> { "https://github.com/sponsors/a", "https://github.com/sponsors/b" } };
            }
        }

        public override IEnumerable<Func<Tuple<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>>> CreateSpecialIndexes
        {
            get
            {
                // A null list and an empty list are both treated as "no sponsorship URLs".
                yield return () => Tuple.Create(
                    new PackageRegistrationIndexMetadata { SponsorshipUrls = null },
                    new PackageRegistrationIndexMetadata { SponsorshipUrls = new List<string>() },
                    true);

                // Ordering does not matter; the URLs are compared as an unordered set.
                yield return () => Tuple.Create(
                    new PackageRegistrationIndexMetadata { SponsorshipUrls = new List<string> { "https://github.com/sponsors/a", "https://github.com/sponsors/b" } },
                    new PackageRegistrationIndexMetadata { SponsorshipUrls = new List<string> { "https://github.com/sponsors/b", "https://github.com/sponsors/a" } },
                    true);

                // Duplicate entries do not change the set.
                yield return () => Tuple.Create(
                    new PackageRegistrationIndexMetadata { SponsorshipUrls = new List<string> { "https://github.com/sponsors/a" } },
                    new PackageRegistrationIndexMetadata { SponsorshipUrls = new List<string> { "https://github.com/sponsors/a", "https://github.com/sponsors/a" } },
                    true);

                // A differing set must fail.
                yield return () => Tuple.Create(
                    new PackageRegistrationIndexMetadata { SponsorshipUrls = new List<string> { "https://github.com/sponsors/a" } },
                    new PackageRegistrationIndexMetadata { SponsorshipUrls = new List<string> { "https://github.com/sponsors/a", "https://github.com/sponsors/b" } },
                    false);
            }
        }

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateSkippedIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => null
        };
    }
}
