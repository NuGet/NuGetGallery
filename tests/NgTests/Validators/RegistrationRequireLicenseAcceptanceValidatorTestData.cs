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
    public class RegistrationRequireLicenseAcceptanceValidatorTestData : RegistrationIndexValidatorTestData<RegistrationRequireLicenseAcceptanceValidator>
    {
        protected override RegistrationRequireLicenseAcceptanceValidator CreateValidator(
            IDictionary<FeedType, SourceRepository> feedToSource, 
            ILogger<RegistrationRequireLicenseAcceptanceValidator> logger)
        {
            return new RegistrationRequireLicenseAcceptanceValidator(feedToSource, logger);
        }
        
        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => new PackageRegistrationIndexMetadata() { RequireLicenseAcceptance = true },
            () => new PackageRegistrationIndexMetadata() { RequireLicenseAcceptance = false }
        };

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateSkippedIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => null
        };
    }
}
