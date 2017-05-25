// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class AggregateNotificationService : IMonitoringNotificationService
    {
        IEnumerable<IMonitoringNotificationService> _notificationServices;

        public AggregateNotificationService(IEnumerable<IMonitoringNotificationService> notificationServices)
        {
            _notificationServices = notificationServices;
        }

        public Task OnPackageValidationFinishedAsync(PackageValidationResult result, CancellationToken token)
        {
            var tasks = new List<Task>();

            foreach (var notificationService in _notificationServices)
            {
                tasks.Add(Task.Run(
                    () => notificationService.OnPackageValidationFinishedAsync(result, token)));
            }

            return Task.WhenAll(tasks);
        }

        public Task OnPackageValidationFailedAsync(string packageId, string packageVersion, IList<JObject> catalogEntriesJson, Exception e, CancellationToken token)
        {
            var tasks = new List<Task>();

            foreach (var notificationService in _notificationServices)
            {
                tasks.Add(Task.Run(
                    () => notificationService.OnPackageValidationFailedAsync(packageId, packageVersion, catalogEntriesJson, e, token)));
            }

            return Task.WhenAll(tasks);
        }
    }
}
