// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public sealed class CatalogProperties
    {
        public DateTime? LastCreated { get; }
        public DateTime? LastDeleted { get; }
        public DateTime? LastEdited { get; }

        public CatalogProperties(DateTime? lastCreated, DateTime? lastDeleted, DateTime? lastEdited)
        {
            LastCreated = lastCreated;
            LastDeleted = lastDeleted;
            LastEdited = lastEdited;
        }

        /// <summary>
        /// Asynchronously reads and returns top-level <see cref="DateTime" /> metadata from the catalog's index.json.
        /// </summary>
        /// <remarks>The metadata values include "nuget:lastCreated", "nuget:lastDeleted", and "nuget:lastEdited",
        /// which are the timestamps of the catalog cursor.</remarks>
        /// <param name="storage"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="telemetryService"></param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="CatalogProperties" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="storage" /> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public static async Task<CatalogProperties> ReadAsync(
            IStorage storage,
            ITelemetryService telemetryService,
            CancellationToken cancellationToken)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            if (telemetryService == null)
            {
                throw new ArgumentNullException(nameof(telemetryService));
            }

            cancellationToken.ThrowIfCancellationRequested();

            DateTime? lastCreated = null;
            DateTime? lastDeleted = null;
            DateTime? lastEdited = null;

            var stopwatch = Stopwatch.StartNew();
            var indexUri = storage.ResolveUri("index.json");
            var json = await storage.LoadStringAsync(indexUri, cancellationToken);

            if (json != null)
            {
                var obj = JObject.Parse(json);
                telemetryService.TrackCatalogIndexReadDuration(stopwatch.Elapsed, indexUri);
                JToken token;

                if (obj.TryGetValue("nuget:lastCreated", out token))
                {
                    lastCreated = token.ToObject<DateTime>().ToUniversalTime();
                }

                if (obj.TryGetValue("nuget:lastDeleted", out token))
                {
                    lastDeleted = token.ToObject<DateTime>().ToUniversalTime();
                }

                if (obj.TryGetValue("nuget:lastEdited", out token))
                {
                    lastEdited = token.ToObject<DateTime>().ToUniversalTime();
                }
            }

            return new CatalogProperties(lastCreated, lastDeleted, lastEdited);
        }
    }
}