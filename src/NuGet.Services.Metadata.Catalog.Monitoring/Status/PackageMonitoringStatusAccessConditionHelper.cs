// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGetGallery;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public static class PackageMonitoringStatusAccessConditionHelper
    {
        public static IAccessCondition FromContent(StorageContent content)
        {
            var eTag = (content as StringStorageContentWithETag)?.ETag;
            if (eTag == null)
            {
                return AccessConditionWrapper.GenerateEmptyCondition();
            }
            else
            {
                return AccessConditionWrapper.GenerateIfMatchCondition(eTag);
            }
        }

        public static void UpdateFromExisting(PackageMonitoringStatus status, PackageMonitoringStatus existingStatus)
        {
            foreach (var state in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
            {
                status.ExistingState[state] = AccessConditionWrapper.GenerateIfNotExistsCondition();
            }

            if (existingStatus != null)
            {
                status.ExistingState[existingStatus.State] = existingStatus.AccessCondition;
            }
        }

        public static IAccessCondition FromUnknown()
        {
            return AccessConditionWrapper.GenerateEmptyCondition();
        }
    }
}
