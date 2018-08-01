// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Dnx;

namespace Ng.Jobs
{
    public class Catalog2DnxJob : LoopingNgJob
    {
        private CommitCollector _collector;
        private ReadWriteCursor _front;
        private ReadCursor _back;
        private Uri _destination;

        public Catalog2DnxJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
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
                   + $"-{Arguments.ClientId} <keyvault-client-id> "
                   + $"-{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                   + $"[-{Arguments.ValidateCertificate} true|false]]] "
                   + $"[-{Arguments.Verbose} true|false] "
                   + $"[-{Arguments.Interval} <seconds>]"
                   + $"[-{Arguments.HttpClientTimeoutInSeconds} <seconds>]"
                   + $"[-{Arguments.StorageSuffix} <suffix for the targeted storage if different than default>]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);
            var contentBaseAddress = arguments.GetOrDefault<string>(Arguments.ContentBaseAddress);
            var storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            var httpClientTimeoutInSeconds = arguments.GetOrDefault<int?>(Arguments.HttpClientTimeoutInSeconds);
            var httpClientTimeout = httpClientTimeoutInSeconds.HasValue ? (TimeSpan?)TimeSpan.FromSeconds(httpClientTimeoutInSeconds.Value) : null;

            Logger.LogInformation("CONFIG source: \"{ConfigSource}\" storage: \"{Storage}\"", source, storageFactory);
            Logger.LogInformation("HTTP client timeout: {Timeout}", httpClientTimeout);

            _collector = new DnxCatalogCollector(
                new Uri(source),
                storageFactory,
                TelemetryService,
                Logger,
                MaxDegreeOfParallelism,
                CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, verbose),
                httpClientTimeout)
            {
                ContentBaseAddress = contentBaseAddress == null ? null : new Uri(contentBaseAddress)
            };

            var storage = storageFactory.Create();
            _front = new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.MinValue);
            _back = MemoryCursor.CreateMax();

            _destination = storageFactory.BaseAddress;
            TelemetryService.GlobalDimensions[TelemetryConstants.Destination] = _destination.AbsoluteUri;
        }

        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            using (Logger.BeginScope($"Logging for {{{TelemetryConstants.Destination}}}", _destination.AbsoluteUri))
            using (TelemetryService.TrackDuration(TelemetryConstants.JobLoopSeconds))
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
}