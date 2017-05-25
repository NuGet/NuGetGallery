// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng.Jobs
{
    public class EndpointMonitoringJob : LoopingNgJob
    {
        private ValidationCollectorFactory _collectorFactory;
        private ValidationCollector _collector;
        private ReadWriteCursor _front;
        private ReadCursor _back;

        public EndpointMonitoringJob(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
            _collectorFactory = new ValidationCollectorFactory(loggerFactory);
        }

        public override string GetUsage()
        {
            return "Usage: ng endpointmonitoring "
                   + $"-{Arguments.Gallery} <v2-feed-address> "
                   + $"-{Arguments.Source} <catalog> "
                   + $"-{Arguments.Index} <index>"
                   + $"-{Arguments.EndpointsToTest} <endpoints-to-test>"
                   + $"-{Arguments.EndpointCursorPrefix}<endpoint-to-test> <endpoint-cursor-address>"
                   + $"-{Arguments.StorageBaseAddress} <storage-base-address> "
                   + $"-{Arguments.StorageType} azure "
                   + $"[-{Arguments.StoragePath} <path>]"
                   + $"[-{Arguments.PackageStatusFolder} <folder>]"
                   + "|"
                   + $"[-{Arguments.StorageAccountName} <azure-acc> "
                   + $"-{Arguments.StorageKeyValue} <azure-key> "
                   + $"-{Arguments.StorageContainer} <azure-container> "
                   + $"-{Arguments.StoragePath} <path> "
                   + $"[-{Arguments.VaultName} <keyvault-name> "
                   + $"-{Arguments.ClientId} <keyvault-client-id> "
                   + $"-{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                   + $"[-{Arguments.ValidateCertificate} true|false]]] "
                   + $"[-{Arguments.StoragePathAuditing} <path>]"
                   + "|"
                   + $"[-{Arguments.StorageAccountNameAuditing} <azure-acc> "
                   + $"-{Arguments.StorageKeyValueAuditing} <azure-key> "
                   + $"-{Arguments.StorageContainerAuditing} <azure-container> "
                   + $"-{Arguments.StoragePathAuditing} <path>] "
                   + $"[-{Arguments.Verbose} true|false] "
                   + $"[-{Arguments.Interval} <seconds>]";
        }
        
        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var gallery = arguments.GetOrThrow<string>(Arguments.Gallery);
            var index = arguments.GetOrThrow<string>(Arguments.Index);
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            if (arguments.GetOrThrow<string>(Arguments.StorageType) != Arguments.AzureStorageType)
            {
                throw new ArgumentException("File storage is not supported!");
            }

            var monitoringStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            var auditingStorageFactory = CommandHelpers.CreateSuffixedStorageFactory("Auditing", arguments, verbose);

            var endpointNames = arguments.GetOrThrow<string>(Arguments.EndpointsToTest).Split(';');
            var endpoints = endpointNames.Select(name => 
                new EndpointFactory.Input(name, new Uri(arguments.GetOrThrow<string>(Arguments.EndpointCursorPrefix + name))));

            var messageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(verbose);
            
            var loggerNotificationService = new LoggerMonitoringNotificationService(
                LoggerFactory.CreateLogger<LoggerMonitoringNotificationService>());

            var statusService = new PackageMonitoringStatusService(
                new NamedStorageFactory(monitoringStorageFactory, arguments.GetOrDefault(Arguments.PackageStatusFolder, Arguments.PackageStatusFolderDefault)), 
                LoggerFactory.CreateLogger<PackageMonitoringStatusService>());

            var statusNotificationService = new PackageMonitoringStatusNotificationService(statusService);

            var aggregateNotificationService = new AggregateNotificationService(
                new IMonitoringNotificationService[] { loggerNotificationService, statusNotificationService });

            var context = _collectorFactory.Create(gallery, index, source, monitoringStorageFactory, auditingStorageFactory, endpoints, messageHandlerFactory, aggregateNotificationService, verbose);

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
