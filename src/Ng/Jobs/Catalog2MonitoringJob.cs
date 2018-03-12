// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring;

namespace Ng.Jobs
{
    /// <summary>
    /// Runs a <see cref="ValidationCollector"/> on the catalog.
    /// The purpose of this job is to queue newly added, modified, or deleted packages for the <see cref="MonitoringProcessor"/> to run validations on.
    /// </summary>
    public class Catalog2MonitoringJob : LoopingNgJob
    {
        private ValidationCollectorFactory _collectorFactory;
        private ValidationCollector _collector;
        private ReadWriteCursor _front;
        private ReadCursor _back;

        public Catalog2MonitoringJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
        {
            _collectorFactory = new ValidationCollectorFactory(LoggerFactory);
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var gallery = arguments.GetOrThrow<string>(Arguments.Gallery);
            var index = arguments.GetOrThrow<string>(Arguments.Index);
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            CommandHelpers.AssertAzureStorage(arguments);

            var monitoringStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);

            var endpointInputs = CommandHelpers.GetEndpointFactoryInputs(arguments);

            var messageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, verbose);

            var statusService = CommandHelpers.GetPackageMonitoringStatusService(arguments, monitoringStorageFactory, LoggerFactory);

            var queue = CommandHelpers.CreateStorageQueue<PackageValidatorContext>(arguments, PackageValidatorContext.Version);

            Logger.LogInformation(
                "CONFIG gallery: {Gallery} index: {Index} storage: {Storage} endpoints: {Endpoints}",
                gallery, index, monitoringStorageFactory, string.Join(", ", endpointInputs.Select(e => e.Name)));

            var context = _collectorFactory.Create(
                queue,
                source,
                monitoringStorageFactory,
                endpointInputs,
                TelemetryService,
                messageHandlerFactory);

            _collector = context.Collector;
            _front = context.Front;
            _back = context.Back;
        }

        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            bool run;
            do
            {
                run = await _collector.Run(_front, _back, cancellationToken);
            }
            while (run);
        }
    }
}