// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Services.Status;
using NuGetGallery;

namespace NuGet.Services.Revalidate
{
    public class HealthService : IHealthService
    {
        private readonly ICoreFileStorageService _storage;
        private readonly HealthConfiguration _config;
        private readonly ILogger<HealthService> _logger;

        public HealthService(
            ICoreFileStorageService storage,
            HealthConfiguration config,
            ILogger<HealthService> logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> IsHealthyAsync()
        {
            using (var stream = await _storage.GetFileAsync(_config.ContainerName, _config.StatusBlobName))
            using (var reader = new StreamReader(stream))
            {
                var json = await reader.ReadToEndAsync();
                var status = JsonConvert.DeserializeObject<ServiceStatus>(json);
                var component = status.ServiceRootComponent.GetByPath(_config.ComponentPath);

                if (component == null)
                {
                    _logger.LogError(
                        "Assuming that the service is unhealthy as the component path {ComponentPath} could not be found",
                        _config.ComponentPath);

                    return false;
                }

                return component.Status == ComponentStatus.Up;
            }
        }
    }
}