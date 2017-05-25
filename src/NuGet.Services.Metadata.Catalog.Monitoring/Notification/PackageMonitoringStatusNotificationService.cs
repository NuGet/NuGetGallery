// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageMonitoringStatusNotificationService : IMonitoringNotificationService
    {
        private IPackageMonitoringStatusService _packageMonitoringStatusService;

        public PackageMonitoringStatusNotificationService(IPackageMonitoringStatusService packageMonitoringStatusService)
        {
            _packageMonitoringStatusService = packageMonitoringStatusService;
        }

        public Task OnPackageValidationFinishedAsync(PackageValidationResult result, CancellationToken token)
        {
            var status = new PackageMonitoringStatus(result);

            return _packageMonitoringStatusService.UpdateAsync(status, token);
        }

        public Task OnPackageValidationFailedAsync(string packageId, string packageVersion, IList<JObject> catalogEntriesJson, Exception e, CancellationToken token)
        {
            var status = new PackageMonitoringStatus(new FeedPackageIdentity(packageId, packageVersion), e);

            return _packageMonitoringStatusService.UpdateAsync(status, token);
        }
    }
}
