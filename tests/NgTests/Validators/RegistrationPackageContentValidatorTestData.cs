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
    public class RegistrationPackageContentValidatorTestData : RegistrationLeafValidatorTestData<RegistrationPackageContentValidator>
    {
        protected override RegistrationPackageContentValidator CreateValidator(
            IDictionary<FeedType, SourceRepository> feedToSource, 
            ILogger<RegistrationPackageContentValidator> logger)
        {
            return new RegistrationPackageContentValidator(feedToSource, logger);
        }

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => new PackageRegistrationIndexMetadata() { PackageContent = "https://api.nuget.org/packages/newtonsoft.json.3.5.8.nupkg" },
            () => new PackageRegistrationIndexMetadata() { PackageContent = "https://api.nuget.org/packages/newtonsoft.json.4.0.1.nupkg" },
            () => new PackageRegistrationIndexMetadata() { PackageContent = "https://www.nuget.org/api/v2/packages/newtonsoft.json/6.0.8" },
            () => new PackageRegistrationIndexMetadata() { PackageContent = "https://www.nuget.org/api/v2/packages/newtonsoft.json/9.0.1" }
        };

        public override IEnumerable<Func<PackageRegistrationIndexMetadata>> CreateSkippedIndexes => new Func<PackageRegistrationIndexMetadata>[]
        {
            () => null
        };

        public override IEnumerable<Func<Tuple<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>>> CreateSpecialIndexes =>
            new Func<Tuple<PackageRegistrationIndexMetadata, PackageRegistrationIndexMetadata, bool>>[]
        {
            () => Tuple.Create(
                new PackageRegistrationIndexMetadata() { PackageContent = "https://api.nuget.org/packages/newtonsoft.json.3.5.8.nupkg" },
                new PackageRegistrationIndexMetadata() { PackageContent = "https://www.nuget.org/api/v2/package/newtonsoft.json/3.5.8" },
                true),

            () => Tuple.Create(
                new PackageRegistrationIndexMetadata() { PackageContent = "https://api.nuget.org/packages/newtonsoft.json.9.0.1.nupkg" },
                new PackageRegistrationIndexMetadata() { PackageContent = "https://www.nuget.org/api/v2/package/newtonsoft.json/9.0.1" },
                true)
        };

        public override IEnumerable<Func<PackageRegistrationLeafMetadata>> CreateLeafs => new Func<PackageRegistrationLeafMetadata>[]
        {
            () => new PackageRegistrationLeafMetadata() { PackageContent = "https://api.nuget.org/packages/newtonsoft.json.3.5.8.nupkg" },
            () => new PackageRegistrationLeafMetadata() { PackageContent = "https://api.nuget.org/packages/newtonsoft.json.4.0.1.nupkg" },
            () => new PackageRegistrationLeafMetadata() { PackageContent = "https://www.nuget.org/api/v2/package/newtonsoft.json/6.0.8" },
            () => new PackageRegistrationLeafMetadata() { PackageContent = "https://www.nuget.org/api/v2/package/newtonsoft.json/9.0.1" }
        };

        public override IEnumerable<Func<PackageRegistrationLeafMetadata>> CreateSkippedLeafs => new Func<PackageRegistrationLeafMetadata>[]
        {
            () => null
        };

        public override IEnumerable<Func<Tuple<PackageRegistrationLeafMetadata, PackageRegistrationLeafMetadata, bool>>> CreateSpecialLeafs =>
            new Func<Tuple<PackageRegistrationLeafMetadata, PackageRegistrationLeafMetadata, bool>>[]
        {
            () => Tuple.Create(
                new PackageRegistrationLeafMetadata() { PackageContent = "https://api.nuget.org/packages/newtonsoft.json.3.5.8.nupkg" },
                new PackageRegistrationLeafMetadata() { PackageContent = "https://www.nuget.org/api/v2/package/Newtonsoft.Json/3.5.8" },
                true),

            () => Tuple.Create(
                new PackageRegistrationLeafMetadata() { PackageContent = "https://api.nuget.org/packages/newtonsoft.json.9.0.1.nupkg" },
                new PackageRegistrationLeafMetadata() { PackageContent = "https://www.nuget.org/api/v2/package/newtonsoft.json/9.0.1" },
                true)
        };
    }
}
