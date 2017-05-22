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
    public class RegistrationIdValidatorTestData : RegistrationIndexValidatorTestData<RegistrationIdValidator>
    {
        protected override RegistrationIdValidator CreateValidator(
            IDictionary<FeedType, SourceRepository> feedToSource, 
            ILogger<RegistrationIdValidator> logger)
        {
            return new RegistrationIdValidator(feedToSource, logger);
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
    }
}
