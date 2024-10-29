// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Services.Status;
using StatusAggregator.Container;

namespace StatusAggregator.Export
{
    public class StatusSerializer : IStatusSerializer
    {
        public const string StatusBlobName = "status.json";

        private readonly IContainerWrapper _container;

        private readonly ILogger<StatusSerializer> _logger;

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings()
        {
            Converters = new List<JsonConverter>() { new StringEnumConverter() },
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Ignore
        };

        public StatusSerializer(
            IContainerWrapper container,
            ILogger<StatusSerializer> logger)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Serialize(DateTime lastBuilt, DateTime lastUpdated, IComponent rootComponent, IEnumerable<Event> recentEvents)
        {
            ServiceStatus status;
            string statusJson;
            _logger.LogInformation("Serializing service status.");
            status = new ServiceStatus(lastBuilt, lastUpdated, rootComponent, recentEvents);
            statusJson = JsonConvert.SerializeObject(status, Settings);

            _logger.LogInformation("Saving service status to blob storage.");
            await _container.SaveBlobAsync(StatusBlobName, statusJson);
        }
    }
}