// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Storage;

namespace Ng.Jobs
{
    /// <summary>
    /// Gets <see cref="PackageMonitoringStatus"/>s that have <see cref="PackageState.Invalid"/> and requeue them to be processed by the <see cref="MonitoringProcessor"/>.
    /// </summary>
    public class Monitoring2MonitoringJob : LoopingNgJob
    {
        private IPackageMonitoringStatusService _statusService;
        private IStorageQueue<PackageValidatorContext> _queue;

        private const int DefaultMaxQueueSize = 100;
        private int _maxRequeueQueueSize;

        public Monitoring2MonitoringJob(
            ILoggerFactory loggerFactory,
            ITelemetryClient telemetryClient,
            IDictionary<string, string> telemetryGlobalDimensions)
            : base(loggerFactory, telemetryClient, telemetryGlobalDimensions)
        {
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);
            _maxRequeueQueueSize = arguments.GetOrDefault(Arguments.MaxRequeueQueueSize, DefaultMaxQueueSize);

            CommandHelpers.AssertAzureStorage(arguments);

            var monitoringStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);

            _statusService = CommandHelpers.GetPackageMonitoringStatusService(arguments, monitoringStorageFactory, LoggerFactory);

            _queue = CommandHelpers.CreateStorageQueue<PackageValidatorContext>(arguments, PackageValidatorContext.Version);
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            var currentMessageCount = await _queue.GetMessageCount(cancellationToken);
            if (currentMessageCount > _maxRequeueQueueSize)
            {
                Logger.LogInformation(
                    "Can't requeue any invalid packages because the queue has too many messages ({CurrentMessageCount} > {MaxRequeueQueueSize})!",
                    currentMessageCount, _maxRequeueQueueSize);
                return;
            }

            var invalidPackages = await _statusService.GetAsync(PackageState.Invalid, cancellationToken);

            await Task.WhenAll(invalidPackages.Select(invalidPackage =>
            {
                try
                {
                    Logger.LogInformation("Requeuing invalid package {PackageId} {PackageVersion}.",
                        invalidPackage.Package.Id, invalidPackage.Package.Version);

                    return _queue.AddAsync(
                        new PackageValidatorContext(invalidPackage),
                        cancellationToken);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Failed to requeue invalid package {PackageId} {PackageVersion}: {Exception}",
                        invalidPackage.Package.Id, invalidPackage.Package.Version, e);

                    return Task.FromResult(0);
                }
            }));
        }
    }
}