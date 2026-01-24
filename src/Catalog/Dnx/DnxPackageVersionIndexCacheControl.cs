// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    public static class DnxPackageVersionIndexCacheControl
    {
        private const string DefaultCacheControlForPackageVersionIndex = "max-age=15";
        private const string BlobNameOfPackageIdsToInclude = "PackageIdsToIncludeForCachingPackageVersionIndex.json";

        public static HashSet<string> PackageIdsToInclude = new HashSet<string>();

        public static string GetCacheControl(string id, ILogger logger)
        {
            if (PackageIdsToInclude.Contains(id))
            {
                logger.LogInformation("Add caching to the package version index of package Id: {id}.", id);

                return DefaultCacheControlForPackageVersionIndex;
            }
            else
            {
                return Constants.NoStoreCacheControl;
            }
        }

        public static async Task LoadPackageIdsToIncludeAsync(IStorage storage, ILogger logger, CancellationToken cancellationToken)
        {
            if (!storage.Exists(BlobNameOfPackageIdsToInclude))
            {
                logger.LogInformation("{BlobName} does not exist.", BlobNameOfPackageIdsToInclude);

                return;
            }

            logger.LogInformation("Loading the list of package Ids from {BlobName}.", BlobNameOfPackageIdsToInclude);

            PackageIdsToInclude = new HashSet<string>();
            string jsonFile = await storage.LoadStringAsync(storage.ResolveUri(BlobNameOfPackageIdsToInclude), cancellationToken);
            if (jsonFile != null)
            {
                JObject obj = JObject.Parse(jsonFile);
                JArray ids = obj["ids"] as JArray;
                if (ids != null)
                {
                    foreach (JToken id in ids)
                    {
                        PackageIdsToInclude.Add(id.ToString().ToLowerInvariant());
                    }
                }
            }

            logger.LogInformation("Loaded the list of package Ids from {BlobName}. There are {Count} package Ids.", BlobNameOfPackageIdsToInclude, PackageIdsToInclude.Count);
        }
    }
}
