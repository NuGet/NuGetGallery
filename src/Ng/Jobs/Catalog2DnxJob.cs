// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Dnx;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Configuration;

namespace Ng.Jobs
{
    public class Catalog2DnxJob : LoopingNgJob
    {
        private CommitCollector _collector;
        private ReadWriteCursor _front;
        private ReadCursor _back;

        public Catalog2DnxJob(ILoggerFactory loggerFactory)
            : base(loggerFactory)
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
                   + $"[-{Arguments.Interval} <seconds>]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);
            var contentBaseAddress = arguments.GetOrDefault<string>(Arguments.ContentBaseAddress);
            var storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);

            Logger.LogInformation("CONFIG source: \"{ConfigSource}\" storage: \"{Storage}\"", source, storageFactory);

            _collector = new DnxCatalogCollector(new Uri(source), storageFactory, CommandHelpers.GetHttpMessageHandlerFactory(verbose))
            {
                ContentBaseAddress = contentBaseAddress == null ? null : new Uri(contentBaseAddress)
            };

            var storage = storageFactory.Create();
            _front = new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.MinValue);
            _back = MemoryCursor.CreateMax();
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
