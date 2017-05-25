// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Runs a <see cref="PackageValidator"/> against collected packages.
    /// </summary>
    public class ValidationCollector : SortingIdVersionCollector
    {
        public ValidationCollector(
            PackageValidator packageValidator, 
            Uri index,
            IMonitoringNotificationService notificationService,
            Func<HttpMessageHandler> handlerFunc = null) 
            : base(index, handlerFunc)
        {
            _packageValidator = packageValidator ?? throw new ArgumentNullException(nameof(packageValidator));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }
        
        private readonly PackageValidator _packageValidator;
        private readonly IMonitoringNotificationService _notificationService;

        protected override async Task ProcessSortedBatch(CollectorHttpClient client, KeyValuePair<FeedPackageIdentity, IList<JObject>> sortedBatch, JToken context, CancellationToken cancellationToken)
        {
            var packageId = sortedBatch.Key.Id;
            var packageVersion = sortedBatch.Key.Version;
            var catalogEntriesJson = sortedBatch.Value;

            try
            {
                var result = await _packageValidator.ValidateAsync(packageId, packageVersion, catalogEntriesJson, client, cancellationToken);
                await _notificationService.OnPackageValidationFinishedAsync(result, cancellationToken);
            }
            catch (Exception e)
            {
                await _notificationService.OnPackageValidationFailedAsync(packageId, packageVersion, catalogEntriesJson, e, cancellationToken);
            }
        }
    }
}
