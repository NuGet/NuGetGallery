// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using WebBackgrounder;

namespace NuGetGallery
{
    public class PackageVulnerabilitiesCacheRefreshJob : Job
    {
        private readonly IPackageVulnerabilitiesCacheService _packageVulnerabilitiesCacheService;
        private IServiceScopeFactory _serviceScopeFactory;

        public PackageVulnerabilitiesCacheRefreshJob(TimeSpan interval, 
            IPackageVulnerabilitiesCacheService packageVulnerabilitiesCacheService,
            IServiceScopeFactory serviceScopeFactory)
            : base("", interval)
        {
            _packageVulnerabilitiesCacheService = packageVulnerabilitiesCacheService;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public override Task Execute()
        {
            return new Task(() => _packageVulnerabilitiesCacheService.RefreshCache(_serviceScopeFactory));
        }
    }
}