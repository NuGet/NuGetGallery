// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Registration;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng.Jobs
{
    public class Catalog2RegistrationJob : LoopingNgJob
    {
        private CommitCollector _collector;
        private ReadWriteCursor _front;
        private ReadCursor _back;

        public Catalog2RegistrationJob(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng catalog2registration "
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
                   + Environment.NewLine
                   + "To compress data in a separate container, add: "
                   + $"-{Arguments.UseCompressedStorage} [true|false] "
                   + $"-{Arguments.CompressedStorageBaseAddress} <storage-base-address> "
                   + $"-{Arguments.CompressedStorageAccountName} <azure-acc> "
                   + $"-{Arguments.CompressedStorageKeyValue} <azure-key> "
                   + $"-{Arguments.CompressedStorageContainer} <azure-container> "
                   + $"-{Arguments.CompressedStoragePath} <path>";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var unlistShouldDelete = arguments.GetOrDefault(Arguments.UnlistShouldDelete, false);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            var contentBaseAddress = arguments.GetOrDefault<string>(Arguments.ContentBaseAddress);

            StorageFactory storageFactoryToUse;

            var storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            var compressedStorageFactory = CommandHelpers.CreateCompressedStorageFactory(arguments, verbose);

            Logger.LogInformation("CONFIG source: \"{ConfigSource}\" storage: \"{Storage}\"", source, storageFactory);

            RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();

            if (compressedStorageFactory != null)
            {
                var secondaryStorageBaseUrlRewriter = new SecondaryStorageBaseUrlRewriter(new List<KeyValuePair<string, string>>
                {
                    // always rewrite storage root url in seconary
                    new KeyValuePair<string, string>(storageFactory.BaseAddress.ToString(), compressedStorageFactory.BaseAddress.ToString())
                });

                var aggregateStorageFactory = new AggregateStorageFactory(
                    storageFactory,
                    new[] { compressedStorageFactory },
                    secondaryStorageBaseUrlRewriter.Rewrite);

                storageFactoryToUse = aggregateStorageFactory;
            }
            else
            {
                storageFactoryToUse = storageFactory;
            }

            _collector = new RegistrationCollector(new Uri(source), storageFactoryToUse, CommandHelpers.GetHttpMessageHandlerFactory(verbose))
            {
                ContentBaseAddress = contentBaseAddress == null
                    ? null
                    : new Uri(contentBaseAddress)
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
