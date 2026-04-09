// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    public static class DnxPackageVersionIndexCacheControl
    {
        private static readonly IList<string> PackageIdsToExclude = new List<string>() {
            "BaseTestPackage",
        }.Select(p => p.ToLowerInvariant()).ToList();

        public static string GetCacheControl(string id, ILogger logger)
        {
            var cacheControl = DnxConstants.DefaultCacheControlOfPackageVersionIndex;
            if (PackageIdsToExclude.Contains(id))
            {
                cacheControl = Constants.NoStoreCacheControl;
            }

            logger.LogInformation("Get cache control: {cacheControl} for the package version index of package Id: {id}.", cacheControl, id);

            return cacheControl;
        }
    }
}
