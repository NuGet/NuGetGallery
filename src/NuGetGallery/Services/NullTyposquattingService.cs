// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Services
{
    public class ExactMatchTyposquattingServiceHelper : ITyposquattingServiceHelper
    {
        public bool IsDistanceLessThanOrEqualToThreshold(string uploadedPackageId, string packageId)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(uploadedPackageId, packageId);
        }

        public bool IsDistanceLessThanOrEqualToThresholdWithNormalizedPackageId(string uploadedPackageId, string normalizedPackageId)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(uploadedPackageId, normalizedPackageId);
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