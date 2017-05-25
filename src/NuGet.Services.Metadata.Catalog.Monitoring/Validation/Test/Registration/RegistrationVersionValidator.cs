// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class RegistrationVersionValidator : RegistrationIndexValidator
    {
        public RegistrationVersionValidator(
            IDictionary<FeedType, SourceRepository> feedToSource, 
            ILogger<RegistrationVersionValidator> logger) 
            : base(feedToSource, logger)
        {
        }

        public override async Task<bool> ShouldRunIndex(ValidationContext data, PackageRegistrationIndexMetadata v2, PackageRegistrationIndexMetadata v3)
        {
            return v2 != null && v3 != null;
        }

        public override async Task CompareIndex(ValidationContext data, PackageRegistrationIndexMetadata v2, PackageRegistrationIndexMetadata v3)
        {
            var isEqual =
                v2.Version ==
                v3.Version;

            if (!isEqual)
            {
                throw new MetadataFieldInconsistencyException<PackageRegistrationIndexMetadata>(
                    v2, v3,
                    nameof(PackageRegistrationIndexMetadata.Version),
                    m => m.Version.ToFullString());
            }
        }
    }
}
