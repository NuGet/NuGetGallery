// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Monitoring;

namespace NgTests
{
    public class RegistrationExistsValidatorTestData : RegistrationLeafValidatorTestData<RegistrationExistsValidator>
    {
        protected override RegistrationExistsValidator CreateValidator(
            IDictionary<FeedType, SourceRepository> feedToSource, 
            ILogger<RegistrationExistsValidator> logger)
        {
            return new RegistrationExistsValidator(feedToSource, logger);
        }

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => new PackageRegistrationIndexMetadata(),
            () => null
        };

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateSkippedIndexes => new Func<PackageRegistrationIndexMetadata>[0];

        public override IEnumerable<Func<PackageRegistrationLeafMetadata>> CreateLeafs => new Func<PackageRegistrationLeafMetadata>[]
        {
            () => new PackageRegistrationLeafMetadata(),
            () => null
        };

        public override IEnumerable<Func<PackageRegistrationLeafMetadata>> CreateSkippedLeafs => new Func<PackageRegistrationLeafMetadata>[0];
    }
}
