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
            var v2PackageContentRedirect = await UriUtils.GetRedirectedRequestMessageUri(data.Client, new Uri(v2.PackageContent));
            var v3PackageContentRedirect = await UriUtils.GetRedirectedRequestMessageUri(data.Client, new Uri(v3.PackageContent));
            
            var isEqual = NormalizeUri(v2PackageContentRedirect) == NormalizeUri(v3PackageContentRedirect);

            if (!isEqual)
            {
                throw new MetadataFieldInconsistencyException<PackageRegistrationLeafMetadata>(
                    v2, v3,
                    nameof(PackageRegistrationLeafMetadata.PackageContent),
                    v2PackageContentRedirect, v3PackageContentRedirect);
            }
        }

        /// <summary>
        /// Returns a <see cref="Uri"/> that is identical to <paramref name="packageContent"/> but with a <see cref="Uri.Scheme"/> of <see cref="Uri.UriSchemeHttps"/> and a <see cref="Uri.Port"/> of 80.
        /// 
        /// This is done because the <see cref="Uri.Scheme"/> and the <see cref="Uri.Port"/> are irrelevant for our validation purposes.
        /// </summary>
        private Uri NormalizeUri(Uri packageContent)
        {
            return new UriBuilder(packageContent)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = 80
            }.Uri;
        }
    }
}
