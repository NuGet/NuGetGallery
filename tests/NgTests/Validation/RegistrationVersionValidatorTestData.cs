// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;

namespace NgTests
{
    public class RegistrationVersionValidatorTestData : RegistrationIndexValidatorTestData<RegistrationVersionValidator>
    {
        protected override RegistrationVersionValidator CreateValidator(
            ILogger<RegistrationVersionValidator> logger)
        {
            var endpoint = ValidatorTestUtility.CreateRegistrationEndpoint();
            var config = ValidatorTestUtility.CreateValidatorConfig();

            return new RegistrationVersionValidator(endpoint, config, logger);
        }

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => new PackageRegistrationIndexMetadata() { Version = new NuGetVersion("1.0.0") },
            () => new PackageRegistrationIndexMetadata() { Version = new NuGetVersion("2.0.0-pre") },
            () => new PackageRegistrationIndexMetadata() { Version = new NuGetVersion("3.4.3+build") },
            () => new PackageRegistrationIndexMetadata() { Version = new NuGetVersion("87.23.11-alpha.9") }
        };

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateSkippedIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => null
        };
    }
}