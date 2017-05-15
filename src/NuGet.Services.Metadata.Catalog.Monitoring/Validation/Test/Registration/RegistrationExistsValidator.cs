// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class RegistrationExistsValidator : RegistrationLeafValidator
    {
        public RegistrationExistsValidator(
            IDictionary<FeedType, SourceRepository> feedToSource, 
            ILogger<RegistrationExistsValidator> logger)
            : base(feedToSource, logger)
        {
        }

        public override Task<bool> ShouldRunLeaf(ValidationContext data, PackageRegistrationLeafMetadata v2, PackageRegistrationLeafMetadata v3)
        {
            return Task.FromResult(true);
        }

        public override async Task CompareLeaf(ValidationContext data, PackageRegistrationLeafMetadata v2, PackageRegistrationLeafMetadata v3)
        {
            var v2Exists = v2 != null;
            var v3Exists = v3 != null;

            if (v2Exists != v3Exists)
            {
                const string existsString = "exists";
                const string doesNotExistString = "doesn't exist";

                throw new MetadataInconsistencyException<PackageRegistrationLeafMetadata>(v2, v3,
                    $"V2 {(v2Exists ? existsString : doesNotExistString)} but V3 {(v3Exists ? existsString : doesNotExistString)}!");
            }
        }
    }
}
