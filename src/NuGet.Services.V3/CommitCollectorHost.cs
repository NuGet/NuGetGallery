// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Services.V3
{
    /// <summary>
    /// This is a minimal integration class between the core of the collectors based on NuGet.Jobs infrastructure and
    /// the overly complex collector infrastructure that we have today.
    /// </summary>
    public class CommitCollectorHost : CommitCollector, ICollector
    {
        private readonly ICommitCollectorLogic _logic;

        public CommitCollectorHost(
            ICommitCollectorLogic logic,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> handlerFunc,
            IOptionsSnapshot<CommitCollectorConfiguration> options) : base(
                new Uri(options.Value.Source),
                telemetryService,
                handlerFunc,
                options.Value.HttpClientTimeout)
        {
            _logic = logic ?? throw new ArgumentNullException(nameof(logic));
        }

        protected override async Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(
            IEnumerable<CatalogCommitItem> catalogItems)
        {
            return await _logic.CreateBatchesAsync(catalogItems);
        }

        protected override async Task<bool> OnProcessBatchAsync(
            CollectorHttpClient client,
            IEnumerable<CatalogCommitItem> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken)
        {
            await _logic.OnProcessBatchAsync(items);

            return true;
        }
    }
}
