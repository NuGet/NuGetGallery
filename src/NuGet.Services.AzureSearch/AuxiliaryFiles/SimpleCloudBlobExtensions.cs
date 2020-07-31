// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.AuxiliaryFiles
{
    internal static class SimpleCloudBlobExtensions
    {
        public static async Task<Stream> OpenReadAsync(this ISimpleCloudBlob blob, IAccessCondition accessCondition)
        {
            return await blob.OpenReadAsync(MapAccessCondition(accessCondition));
        }

        public static async Task<Stream> OpenWriteAsync(this ISimpleCloudBlob blob, IAccessCondition accessCondition)
        {
            return await blob.OpenWriteAsync(MapAccessCondition(accessCondition));
        }

        private static AccessCondition MapAccessCondition(IAccessCondition accessCondition)
        {
            return new AccessCondition
            {
                IfNoneMatchETag = accessCondition.IfNoneMatchETag,
                IfMatchETag = accessCondition.IfMatchETag,
            };
        }
    }
}
