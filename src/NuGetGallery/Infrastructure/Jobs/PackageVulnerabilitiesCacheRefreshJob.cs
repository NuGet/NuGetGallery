// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using WebBackgrounder;

namespace NuGetGallery
{
    public class PackageVulnerabilitiesCacheRefreshJob : Job
    {
        private readonly PackageVulnerabilitiesCacheService _packageVulnerabilitiesCacheService;

        public PackageVulnerabilitiesCacheRefreshJob(TimeSpan interval, PackageVulnerabilitiesCacheService packageVulnerabilitiesCacheService)
            : base("", interval)
        {
            _packageVulnerabilitiesCacheService = packageVulnerabilitiesCacheService;
        }

        public override Task Execute()
        {
            return new Task(() => _packageVulnerabilitiesCacheService.RefreshCache());
        }
    }
}