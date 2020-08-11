// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Monitoring;

namespace NgTests
{
    public class RegistrationExistsValidatorTestData : RegistrationLeafValidatorTestData<RegistrationExistsValidator>
    {
        protected override RegistrationExistsValidator CreateValidator(
            ILogger<RegistrationExistsValidator> logger)
        {
            var endpoint = ValidatorTestUtility.CreateRegistrationEndpoint();
            var config = ValidatorTestUtility.CreateValidatorConfig();

            return new RegistrationExistsValidator(endpoint, config, logger);
        }

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => null
        };

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateSkippedIndexes => new Func<PackageRegistrationIndexMetadata>[0];

        public override IEnumerable<Func<Tuple<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>>> CreateSpecialIndexes => new Func<Tuple<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>>[]
        {
            () => Tuple.Create(
                new PackageRegistrationIndexMetadata(),
                new PackageRegistrationIndexMetadata(),
                true),

            () => Tuple.Create<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>(
                null,
                new PackageRegistrationIndexMetadata(),
                false),

            () => Tuple.Create<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>(
                new PackageRegistrationIndexMetadata(),
                null,
                false)
        };

        public override IEnumerable<Func<PackageRegistrationLeafMetadata>> CreateLeafs => new Func<PackageRegistrationLeafMetadata>[]
        {
            () => null
        };

        public override IEnumerable<Func<PackageRegistrationLeafMetadata>> CreateSkippedLeafs => new Func<PackageRegistrationLeafMetadata>[0];

        public override IEnumerable<Func<Tuple<PackageRegistrationLeafMetadata, PackageRegistrationLeafMetadata, bool>>> CreateSpecialLeafs => new Func<Tuple<PackageRegistrationLeafMetadata, PackageRegistrationLeafMetadata, bool>>[]
        {
            () => Tuple.Create(
                new PackageRegistrationLeafMetadata(),
                new PackageRegistrationLeafMetadata(),
                true),

            () => Tuple.Create<PackageRegistrationLeafMetadata, PackageRegistrationLeafMetadata, bool>(
                null,
                new PackageRegistrationLeafMetadata(),
                true),

            () => Tuple.Create<PackageRegistrationLeafMetadata, PackageRegistrationLeafMetadata, bool>(
                new PackageRegistrationLeafMetadata(),
                null,
                false)
        };
    }
}