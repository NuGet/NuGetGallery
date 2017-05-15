// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class RegistrationPackageContentValidator : RegistrationLeafValidator
    {
        public RegistrationPackageContentValidator(
            IDictionary<FeedType, SourceRepository> feedToSource, 
            ILogger<RegistrationPackageContentValidator> logger) 
            : base(feedToSource, logger)
        {
        }

        public override async Task<bool> ShouldRunLeaf(ValidationContext data, PackageRegistrationLeafMetadata v2, PackageRegistrationLeafMetadata v3)
        {
            return v2 != null && v3 != null;
        }

        public override async Task CompareLeaf(ValidationContext data, PackageRegistrationLeafMetadata v2, PackageRegistrationLeafMetadata v3)
        {
            var isEqual = 
                await UriUtils.GetRedirectedRequestMessageUri(data.Client, new Uri(v2.PackageContent)) == 
                await UriUtils.GetRedirectedRequestMessageUri(data.Client, new Uri(v3.PackageContent));

            if (!isEqual)
            {
                throw new MetadataInconsistencyException<PackageRegistrationLeafMetadata>(v2, v3, 
                    $"{nameof(PackageRegistrationLeafMetadata.PackageContent)} does not match!");
            }
        }
    }
}
