// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Versioning;

namespace NgTests
{
    public class RegistrationVersionValidatorTestData : RegistrationIndexValidatorTestData<RegistrationVersionValidator>
    {
        protected override RegistrationVersionValidator CreateValidator(
            IDictionary<FeedType, SourceRepository> feedToSource, 
            ILogger<RegistrationVersionValidator> logger)
        {
            return new RegistrationVersionValidator(feedToSource, logger);
        }

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => new PackageRegistrationIndexMetadata() { Version = new NuGetVersion(1, 0, 0) },
            () => new PackageRegistrationIndexMetadata() { Version = new NuGetVersion(2, 0, 0) }
        };

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateSkippedIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => null
        };
    }
}
