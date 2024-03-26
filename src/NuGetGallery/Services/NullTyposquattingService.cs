// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Services
{
    public class ExactMatchTyposquattingServiceHelper : ITyposquattingServiceHelper
    {
        public bool IsDistanceLessThanOrEqualToThreshold(string uploadedPackageId, string packageId)
        {
            return uploadedPackageId.ToLowerInvariant() == packageId.ToLowerInvariant();
        }

        public bool IsDistanceLessThanOrEqualToThresholdWithNormalizedPackageId(string uploadedPackageId, string normalizedPackageId)
        {
            return uploadedPackageId.ToLowerInvariant() == normalizedPackageId.ToLowerInvariant();
        }

        public string NormalizeString(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                return string.Empty;
            }

            return packageId.ToLowerInvariant();
        }
    }
}