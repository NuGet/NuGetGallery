// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Blobs.Models;
using Azure;
using NuGetGallery;

namespace NuGet.Services.Metadata.Catalog.Extensions
{
    public static class AccessConditionExtensions
    {
        public static BlobRequestConditions ToBlobRequestConditions(this IAccessCondition accessCondition)
        {
            return new BlobRequestConditions
            {
                IfMatch = accessCondition.IfMatchETag != null ? new ETag(accessCondition.IfMatchETag) : null,
                IfNoneMatch = accessCondition.IfNoneMatchETag != null ? new ETag(accessCondition.IfNoneMatchETag) : null
            };
        }
    }
}
