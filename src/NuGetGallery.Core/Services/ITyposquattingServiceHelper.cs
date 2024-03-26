// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// This interface for providing additional methods for ITyposquattingService.
    /// </summary>
    public interface ITyposquattingServiceHelper
    {
        /// <summary>
        /// This method is used to check if the distance between the currently uploaded package ID and another package ID is less than or equal to the threshold.
        /// </summary>
        /// <param name="uploadedPackageId">Uploaded package Id</param>
        /// <param name="packageId">Package Id compared to</param>
        /// <returns>Return true if distance is less than the threshold</returns>
        bool IsDistanceLessThanOrEqualToThreshold(string uploadedPackageId, string packageId);

        /// <summary>
        /// This method is used to check if the distance between the currently uploaded package ID and another package ID is less than or equal to the threshold.
        /// </summary>
        /// <param name="uploadedPackageId">Uploaded package Id</param>
        /// <param name="normalizedPackageId">Normalized Package Id compared to</param>
        /// <returns>Return true if distance is less than the threshold</returns>
        bool IsDistanceLessThanOrEqualToThresholdWithNormalizedPackageId(string uploadedPackageId, string normalizedPackageId);

        /// <summary>
        /// This method is used to normalize string.
        /// </summary>
        /// <param name="str">String to normalize</param>
        /// <returns>Normalized string</returns>
        string NormalizeString(string str);
    }
}