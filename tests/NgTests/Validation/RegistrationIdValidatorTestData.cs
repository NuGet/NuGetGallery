// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Monitoring;

namespace NgTests
{
    public class RegistrationIdValidatorTestData : RegistrationIndexValidatorTestData<RegistrationIdValidator>
    {
        protected override RegistrationIdValidator CreateValidator(
            ILogger<RegistrationIdValidator> logger)
        {
            var endpoint = ValidatorTestUtility.CreateRegistrationEndpoint();
            var config = ValidatorTestUtility.CreateValidatorConfig();

            return new RegistrationIdValidator(endpoint, config, logger);
        }

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => new PackageRegistrationIndexMetadata() { Id = "testPackage1" },
            () => new PackageRegistrationIndexMetadata() { Id = "testPackage2" }
        };

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateSkippedIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => null
        };

        public override IEnumerable<Func<Tuple<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>>> CreateSpecialIndexes => new Func<Tuple<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>>[]
        {
            () => Tuple.Create(
                new PackageRegistrationIndexMetadata() { Id = "testPackage1" },
                new PackageRegistrationIndexMetadata() { Id = "testpackage1" },
                true),

            () => Tuple.Create(
                new PackageRegistrationIndexMetadata() { Id = "testpackage1" },
                new PackageRegistrationIndexMetadata() { Id = "testPackage1" },
                true)
        };
    }
}