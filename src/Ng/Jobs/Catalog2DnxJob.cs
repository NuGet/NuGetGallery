// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol.Catalog;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Dnx;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng.Jobs
{
    public class Catalog2DnxJob : LoopingNgJob
    {
        private CommitCollector _collector;
        private ReadWriteCursor _front;
        private ReadCursor _back;
        private Uri _destination;

        public Catalog2DnxJob(ILoggerFactory loggerFactory,
            ITelemetryClient telemetryClient,
            IDictionary<string, string> telemetryGlobalDimensions)
            : base(loggerFactory, telemetryClient, telemetryGlobalDimensions)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng catalog2dnx "
                   + $"-{Arguments.Source} <catalog> "
                   + $"-{Arguments.ContentBaseAddress} <content-address> "
                   + $"-{Arguments.StorageBaseAddress} <storage-base-address> "
                   + $"-{Arguments.StorageType} file|azure "
                   + $"[-{Arguments.StoragePath} <path>]"
                   + "|"
                   + $"[-{Arguments.StorageAccountName} <azure-acc>"
                   + $"-{Arguments.StorageKeyValue} <azure-key> "
                   + $"-{Arguments.StorageContainer} <azure-container> "
                   + $"-{Arguments.StoragePath} <path> "
                   + $"[-{Arguments.VaultName} <keyvault-name> "
                   + $"-{Arguments.UseManagedIdentity} true|false "
                   + $"-{Arguments.ClientId} <keyvault-client-id> Should not be set if {Arguments.UseManagedIdentity} is true"
                   + $"-{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> Should not be set if {Arguments.UseManagedIdentity} is true"
                   + $"[-{Arguments.ValidateCertificate} true|false]]] "
                   + $"[-{Arguments.Verbose} true|false] "
                   + $"[-{Arguments.Interval} <seconds>]"
                   + $"[-{Arguments.HttpClientTimeoutInSeconds} <seconds>]"
                   + $"[-{Arguments.StorageSuffix} <suffix for the targeted storage if different than default>]"
                   + $"[-{Arguments.PreferAlternatePackageSourceStorage} true|false "
                   + $"-{Arguments.StorageAccountNamePreferredPackageSourceStorage} <azure-acc> "
                   + $"-{Arguments.StorageKeyValuePreferredPackageSourceStorage} <azure-key> "
                   + $"-{Arguments.StorageContainerPreferredPackageSourceStorage} <azure-container>"
                   + $"-{Arguments.StorageUseServerSideCopy} true|false]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);
            var contentBaseAddress = arguments.GetOrDefault<string>(Arguments.ContentBaseAddress);
            var storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            var httpClientTimeoutInSeconds = arguments.GetOrDefault<int?>(Arguments.HttpClientTimeoutInSeconds);
            var httpClientTimeout = httpClientTimeoutInSeconds.HasValue ? (TimeSpan?)TimeSpan.FromSeconds(httpClientTimeoutInSeconds.Value) : null;

            StorageFactory preferredPackageSourceStorageFactory = null;
            IAzureStorage preferredPackageSourceStorage = null;

            var preferAlternatePackageSourceStorage = arguments.GetOrDefault(Arguments.PreferAlternatePackageSourceStorage, defaultValue: false);

            if (preferAlternatePackageSourceStorage)
            {
                preferredPackageSourceStorageFactory = CommandHelpers.CreateSuffixedStorageFactory("PreferredPackageSourceStorage", arguments, verbose);
                preferredPackageSourceStorage = preferredPackageSourceStorageFactory.Create() as IAzureStorage;
            }

            Logger.LogInformation("CONFIG source: \"{ConfigSource}\" storage: \"{Storage}\" preferred package source storage: \"{PreferredPackageSourceStorage}\"",
                source,
                storageFactory,
                preferredPackageSourceStorageFactory);
            Logger.LogInformation("HTTP client timeout: {Timeout}", httpClientTimeout);

            MaxDegreeOfParallelism = 256;

            _collector = new DnxCatalogCollector(
                new Uri(source),
                storageFactory,
                preferredPackageSourceStorage,
                contentBaseAddress == null ? null : new Uri(contentBaseAddress),
                TelemetryService,
                Logger,
                MaxDegreeOfParallelism,
                httpClient => new CatalogClient(new SimpleHttpClient(httpClient, LoggerFactory.CreateLogger<SimpleHttpClient>()), LoggerFactory.CreateLogger<CatalogClient>()),
                CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, verbose),
                httpClientTimeout);

            var storage = storageFactory.Create();
            _front = new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.MinValue);
            _back = MemoryCursor.CreateMax();

            _destination = storageFactory.BaseAddress;
            TelemetryService.GlobalDimensions[TelemetryConstants.Destination] = _destination.AbsoluteUri;
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            using (Logger.BeginScope($"Logging for {{{TelemetryConstants.Destination}}}", _destination.AbsoluteUri))
            using (TelemetryService.TrackDuration(TelemetryConstants.JobLoopSeconds))
            {
                bool run;
                do
                {
                    run = await _collector.RunAsync(_front, _back, cancellationToken);
                }
                while (run);
            }
        }
    }
}