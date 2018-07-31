// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Jobs.Extensions;
using NuGet.Services.Status;
using NuGet.Services.Status.Table;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class StatusExporter : IStatusExporter
    {
        private const string StatusBlobName = "status.json";
        private readonly TimeSpan _eventVisibilityPeriod;

        private readonly CloudBlobContainer _container;
        private readonly ITableWrapper _table;

        private readonly ILogger<StatusExporter> _logger;

        private static readonly JsonSerializerSettings _statusBlobJsonSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new StatusContractResolver(),
            Converters = new List<JsonConverter>() { new StringEnumConverter() },
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Ignore
        };

        public StatusExporter(
            CloudBlobContainer container, 
            ITableWrapper table,
            StatusAggregatorConfiguration configuration,
            ILogger<StatusExporter> logger)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _eventVisibilityPeriod = TimeSpan.FromDays(configuration?.EventVisibilityPeriodDays ?? throw new ArgumentNullException(nameof(configuration)));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ServiceStatus> Export()
        {
            using (_logger.Scope("Exporting service status."))
            {
                var rootComponent = NuGetServiceComponentFactory.CreateNuGetServiceRootComponent();

                var recentEvents = _table
                    .CreateQuery<EventEntity>()
                    .Where(e =>
                        e.PartitionKey == EventEntity.DefaultPartitionKey &&
                        (e.IsActive || (e.EndTime >= DateTime.UtcNow - _eventVisibilityPeriod)))
                    .ToList()
                    .Select(e =>
                    {
                        var messages = _table.GetMessagesLinkedToEvent(e)
                            .ToList()
                            .Select(m => m.AsMessage());
                        return e.AsEvent(messages);
                    })
                    .Where(e => e.Messages != null && e.Messages.Any());

                // If multiple events are affecting a single region, the event with the highest severity should affect the component.
                var activeEvents = recentEvents
                    .Where(e => e.EndTime == null || e.EndTime >= DateTime.UtcNow)
                    .GroupBy(e => e.AffectedComponentPath)
                    .Select(g => g.OrderByDescending(e => e.AffectedComponentStatus).First());

                foreach (var activeEvent in activeEvents)
                {
                    using (_logger.Scope("Applying active event affecting '{AffectedComponentPath}' of severity {AffectedComponentStatus} at {StartTime} to root component", 
                        activeEvent.AffectedComponentPath, activeEvent.AffectedComponentStatus, activeEvent.StartTime))
                    {
                        var currentComponent = rootComponent.GetByPath(activeEvent.AffectedComponentPath);

                        if (currentComponent == null)
                        {
                            _logger.LogWarning("Couldn't find component corresponding to active event.");
                            continue;
                        }

                        currentComponent.Status = activeEvent.AffectedComponentStatus;
                    }
                }

                ServiceStatus status;
                string statusJson;
                using (_logger.Scope("Serializing service status."))
                {
                    status = new ServiceStatus(rootComponent, recentEvents);
                    statusJson = JsonConvert.SerializeObject(status, _statusBlobJsonSerializerSettings);
                }

                using (_logger.Scope("Saving service status to blob storage."))
                {
                    var blob = _container.GetBlockBlobReference(StatusBlobName);
                    await blob.UploadTextAsync(statusJson);
                }

                return status;
            }
        }
    }
}
