// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using NuGet.Services.Entities;
using NuGetGallery;

namespace UpdateBlobProperties
{
    public class BlobInfoOfPackageVersionIndexInFlatContainer : BlobInfo
    {
        public override string GetContainerName()
        {
            return "v3-flatcontainer";
        }

        public override string GetBlobName(PackageInfo packageInfo)
        {
            if (packageInfo == null)
            {
                throw new ArgumentNullException(nameof(packageInfo));
            }

            if (string.IsNullOrWhiteSpace(packageInfo.Id))
            {
                throw new ArgumentException($"Invalid package Id with null or whitespace. Package Key: {packageInfo.Key}.");
            }

            return $"/{packageInfo.Id.ToLowerInvariant()}/index.json";
        }

        public override IDictionary<string, string> GetUpdatedBlobProperties()
        {
            return new Dictionary<string, string>()
            {
                { nameof(BlobHttpHeaders.CacheControl), "max-age=10" }
            };
        }

        public override Func<IEntityRepository<Package>, int, int, int, Task<List<PackageInfo>>> GetPageOfPackageInfosToUpdateBlobsAsync
        {
            get
            {
                return PackageInfo.GetPageOfPackageInfosWithIdOrderedByPackageRegistrationKeyAsync;
            }
        }
    }
}
