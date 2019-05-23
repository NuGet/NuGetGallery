// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Monitoring.PackageLag;
using NuGet.Services.AzureManagement;

namespace NuGet.Monitoring.RebootSearchInstance
{
    public class SearchInstanceRebooter : ISearchInstanceRebooter
    {
        private readonly IFeedClient _feedClient;
        private readonly ISearchServiceClient _searchServiceClient;
        private readonly IAzureManagementAPIWrapper _azureManagementAPIWrapper;
        private readonly ITelemetryService _telemetryService;
        private readonly IOptionsSnapshot<MonitorConfiguration> _configuration;
        private readonly ILogger<SearchInstanceRebooter> _logger;
        private readonly TimeSpan _healthyThreshold;
        private readonly TimeSpan _unhealthyThreshold;

        public SearchInstanceRebooter(
            IFeedClient feedClient,
            ISearchServiceClient searchServiceClient,
            IAzureManagementAPIWrapper azureManagementAPIWrapper,
            ITelemetryService telemetryService,
            IOptionsSnapshot<MonitorConfiguration> configuration,
            ILogger<SearchInstanceRebooter> logger)
        {
            _feedClient = feedClient ?? throw new ArgumentNullException(nameof(feedClient));
            _searchServiceClient = searchServiceClient ?? throw new ArgumentNullException(nameof(searchServiceClient));
            _azureManagementAPIWrapper = azureManagementAPIWrapper ?? throw new ArgumentNullException(nameof(azureManagementAPIWrapper));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _healthyThreshold = TimeSpan.FromSeconds(_configuration.Value.HealthyThresholdInSeconds);
            _unhealthyThreshold = TimeSpan.FromSeconds(_configuration.Value.UnhealthyThresholdInSeconds);
        }

        public async Task RunAsync(CancellationToken token)
        {
            var tasks = _configuration
                .Value
                .RegionInformations
                .Select(r => ProcessRegionLoopAsync(r, token))
                .ToList();

            await Task.WhenAll(tasks);
        }

        private async Task ProcessRegionLoopAsync(RegionInformation regionInformation, CancellationToken token)
        {
            using (_logger.BeginScope(
                "Starting search instance check loop for region {Region}, service {ServiceName}.",
                regionInformation.Region,
                regionInformation.ServiceName))
            {
                var stopwatch = Stopwatch.StartNew();

                do
                {
                    try
                    {
                        _logger.LogInformation("Checking region {Region} for search instances that need rebooting.", regionInformation.Region);

                        await ProcessRegionOnceAsync(regionInformation, token);

                        _logger.LogInformation(
                            "Sleeping for {SleepDuration} before checking region {Region} again.",
                            _configuration.Value.SleepDuration,
                            regionInformation.Region);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            (EventId)0,
                            ex,
                            "An exception was thrown when processing region {Region}.", regionInformation.Region);
                    }

                    await Task.Delay(_configuration.Value.SleepDuration);
                }
                while (stopwatch.Elapsed < _configuration.Value.ProcessLifetime);
            }
        }

        private async Task ProcessRegionOnceAsync(RegionInformation regionInformation, CancellationToken token)
        {
            var instances = await _searchServiceClient.GetSearchEndpointsAsync(regionInformation, token);
            var latestFeedTimeStamp = await _feedClient.GetLatestFeedTimeStampAsync();

            _logger.LogDebug("The latest feed timestamp is {LastFeedTimeStamp}.", latestFeedTimeStamp);

            // Determine the health of all instances.
            var tasks = instances
                .Select(i => GetInstanceAndHealthAsync(latestFeedTimeStamp, i, token))
                .ToList();
            var instancesAndHealths = await Task.WhenAll(tasks);

            var healthyCount = instancesAndHealths.Count(x => x.Health == InstanceHealth.Healthy);
            var unhealthyCount = instancesAndHealths.Count(x => x.Health == InstanceHealth.Unhealthy);

            if (healthyCount == 0)
            {
                _logger.LogWarning(
                    "There are no healthy instances on {Region}. No instances will be restarted since there is " +
                    "likely a broader issue going on.",
                    regionInformation.Region);
            }
            else if (unhealthyCount == 0)
            {
                _logger.LogInformation(
                    "There are no unhealthy instances on {Region}. No more work is necessary.",
                    regionInformation.Region);
            }
            else
            {
                // Restart the first unhealthy instance and wait for it to come back.
                var unhealthyInstance = instancesAndHealths
                    .Where(i => i.Health == InstanceHealth.Unhealthy)
                    .OrderBy(i => i.Instance.Index)
                    .FirstOrDefault();

                await RestartInstanceAndWaitForHealthyAsync(
                    regionInformation,
                    latestFeedTimeStamp,
                    unhealthyInstance.Instance,
                    token);
            }

            // Emit telemetry
            _telemetryService.TrackHealthyInstanceCount(regionInformation.Region, healthyCount);
            _telemetryService.TrackUnhealthyInstanceCount(regionInformation.Region, unhealthyCount);
            _telemetryService.TrackUnknownInstanceCount(
                regionInformation.Region,
                instancesAndHealths.Count(x => x.Health == InstanceHealth.Unknown));
            _telemetryService.TrackInstanceCount(
                regionInformation.Region,
                instancesAndHealths.Count());
        }

        private async Task RestartInstanceAndWaitForHealthyAsync(
            RegionInformation regionInformation,
            DateTimeOffset latestFeedTimeStamp,
            Instance instance,
            CancellationToken token)
        {
            _telemetryService.TrackInstanceReboot(regionInformation.Region, instance.Index);

            var roleInstance = string.Format(_configuration.Value.RoleInstanceFormat, instance.Index);

            _logger.LogWarning("Rebooting role instance {RoleInstance} in region {Region}.", roleInstance, regionInformation.Region);

            await _azureManagementAPIWrapper.RebootCloudServiceRoleInstanceAsync(
                _configuration.Value.Subscription,
                regionInformation.ResourceGroup,
                regionInformation.ServiceName,
                instance.Slot,
                _configuration.Value.Role,
                roleInstance,
                token);

            InstanceHealth health;
            var waitStopwatch = Stopwatch.StartNew();
            do
            {
                _logger.LogDebug(
                    "Checking the health status of role instance {RoleInstance} after rebooting.",
                    roleInstance);

                health = await DetermineInstanceHealthAsync(latestFeedTimeStamp, instance, token);
                if (health != InstanceHealth.Healthy)
                {
                    await Task.Delay(_configuration.Value.InstancePollFrequency);
                }
            }
            while (waitStopwatch.Elapsed < _configuration.Value.WaitForHealthyDuration
                   && health != InstanceHealth.Healthy);

            waitStopwatch.Stop();
            _logger.LogInformation(
                "After waiting {WaitDuration}, instance {DiagUrl} has health of {Health}.",
                waitStopwatch.Elapsed,
                instance.DiagUrl,
                health);
            _telemetryService.TrackInstanceRebootDuration(
                regionInformation.Region,
                instance.Index,
                waitStopwatch.Elapsed,
                health);
        }

        private async Task<InstanceAndHealth> GetInstanceAndHealthAsync(
            DateTimeOffset latestFeedTimeStamp,
            Instance instance,
            CancellationToken token)
        {
            var health = await DetermineInstanceHealthAsync(latestFeedTimeStamp, instance, token);

            _logger.LogInformation(
                "Instance {DiagUrl} has health of {Health}.",
                instance.DiagUrl,
                health);

            return new InstanceAndHealth(instance, health);
        }

        private async Task<InstanceHealth> DetermineInstanceHealthAsync(
            DateTimeOffset latestFeedTimeStamp,
            Instance instance,
            CancellationToken token)
        {
            DateTimeOffset commitDateTime;
            try
            {
                commitDateTime = await _searchServiceClient.GetCommitDateTimeAsync(instance, token);
            }
            catch (HttpResponseException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable
                                                || ex.StatusCode == HttpStatusCode.InternalServerError)
            {
                _logger.LogInformation(
                    (EventId)0,
                    ex,
                    "The HTTP response when hitting {DiagUrl} was {StatusCode} {ReasonPhrase}. Considering this " +
                    "instance as an unhealthy state.",
                    instance.DiagUrl,
                    (int)ex.StatusCode,
                    ex.ReasonPhrase);

                return InstanceHealth.Unhealthy;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(
                    (EventId)0,
                    ex,
                    "An exception was thrown when getting the commit timestamp from {DiagUrl}. Considering this " +
                    "instance as an unknown state.",
                    instance.DiagUrl);

                return InstanceHealth.Unknown;
            }

            var lag = latestFeedTimeStamp - commitDateTime;

            _logger.LogDebug(
                "Instance {DiagUrl} has commit timestamp {CommitTimeStamp} and lag of {Lag}.",
                instance.DiagUrl,
                commitDateTime,
                lag);

            if (lag <= _healthyThreshold)
            {
                return InstanceHealth.Healthy;
            }

            if (lag > _unhealthyThreshold)
            {
                return InstanceHealth.Unhealthy;
            }

            return InstanceHealth.Unknown;
        }

        private class InstanceAndHealth
        {
            public InstanceAndHealth(Instance instance, InstanceHealth health)
            {
                Instance = instance;
                Health = health;
            }

            public Instance Instance { get; }
            public InstanceHealth Health { get; }
        }
    }
}
