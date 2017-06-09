// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class RegistrationListedValidator : RegistrationLeafValidator
    {
        public RegistrationListedValidator(
            IDictionary<FeedType, SourceRepository> feedToSource, 
            ILogger<RegistrationListedValidator> logger) 
            : base(feedToSource, logger)
        {
        }

        public override async Task<bool> ShouldRunLeaf(ValidationContext data, PackageRegistrationLeafMetadata v2, PackageRegistrationLeafMetadata v3)
        {
            return v2 != null && v3 != null;
        }

        public override async Task CompareLeaf(ValidationContext data, PackageRegistrationLeafMetadata v2, PackageRegistrationLeafMetadata v3)
        {
            if (v2.Listed != v3.Listed)
            {
                throw new MetadataFieldInconsistencyException<PackageRegistrationLeafMetadata>(
                    v2, v3, 
                    nameof(PackageRegistrationLeafMetadata.Listed),
                    m => m.Listed);
            }
        }
    }
}
