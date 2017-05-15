// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring;

namespace Ng.Jobs
{
    public class EndpointMonitoringJob : LoopingNgJob
    {
        private ValidationCollector _collector;
        private ReadWriteCursor _front;
        private ReadCursor _back;

        public EndpointMonitoringJob(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng endpointmonitoring "
                   + $"-{Arguments.Gallery} <v2-feed-address> "
                   + $"-{Arguments.Source} <catalog> "
                   + $"-{Arguments.EndpointsToTest} <endpoints-to-test>"
                   + $"-{Arguments.EndpointCursorPrefix}<endpoint-to-test> <endpoint-cursor-address>"
                   + $"-{Arguments.StorageBaseAddress} <storage-base-address> "
                   + $"-{Arguments.StorageType} azure "
                   + $"[-{Arguments.StoragePath} <path>]"
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

        private IList<Lazy<INuGetResourceProvider>> ResourceProvidersToInjectV2 => new List<Lazy<INuGetResourceProvider>>
        {
            new Lazy<INuGetResourceProvider>(() => new NonhijackableV2HttpHandlerResourceProvider()),
            new Lazy<INuGetResourceProvider>(() => new PackageTimestampMetadataResourceV2Provider(LoggerFactory)),
            new Lazy<INuGetResourceProvider>(() => new PackageRegistrationMetadataResourceV2FeedProvider())
        };

        private IList<Lazy<INuGetResourceProvider>> ResourceProvidersToInjectV3 => new List<Lazy<INuGetResourceProvider>>
        {
            new Lazy<INuGetResourceProvider>(() => new PackageRegistrationMetadataResourceV3Provider())
        };

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

            var storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            var auditingStorageFactory = CommandHelpers.CreateSuffixedStorageFactory("Auditing", arguments, verbose);

            var endpointNames = arguments.GetOrThrow<string>(Arguments.EndpointsToTest).Split(';');

            Logger.LogInformation(
                "CONFIG gallery: {Gallery}  source: {ConfigSource} storage: {Storage} auditingStorage: {AuditingStorage} endpoints: {Endpoints}",
                gallery, source, storageFactory, auditingStorageFactory, string.Join(", ", endpointNames));

            var messageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(verbose);

            var feedToSource = new Dictionary<FeedType, SourceRepository>()
            {
                {FeedType.HttpV2, new SourceRepository(new PackageSource(gallery), GetResourceProviders(ResourceProvidersToInjectV2), FeedType.HttpV2)},
                {FeedType.HttpV3, new SourceRepository(new PackageSource(index), GetResourceProviders(ResourceProvidersToInjectV3), FeedType.HttpV3)}
            };

            var validationFactory = new ValidatorFactory(
                feedToSource,
                LoggerFactory);

            var endpointFactory = new EndpointFactory(
                validationFactory, 
                endpointName => new Uri(arguments.GetOrThrow<string>(Arguments.EndpointCursorPrefix + endpointName)),
                messageHandlerFactory,
                LoggerFactory);

            var endpoints = endpointNames.Select(endpointName => endpointFactory.Create(endpointName));

            _collector = new ValidationCollector(auditingStorageFactory.Create(),
                new PackageValidator(endpoints, LoggerFactory.CreateLogger<PackageValidator>()), 
                new Uri(source),
                LoggerFactory.CreateLogger<ValidationCollector>(),
                messageHandlerFactory);

            var storage = storageFactory.Create();
            _front = new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.MinValue);
            _back = new AggregateCursor(endpoints.Select(e => e.Cursor));
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

        private IEnumerable<Lazy<INuGetResourceProvider>> GetResourceProviders(IList<Lazy<INuGetResourceProvider>> providersToInject)
        {
            var resourceProviders = Repository.Provider.GetCoreV3().ToList();
            resourceProviders.AddRange(providersToInject);
            return resourceProviders;
        }
    }
}
