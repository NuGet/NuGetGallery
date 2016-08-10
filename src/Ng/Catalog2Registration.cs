// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ng
{
    public class Catalog2Registration
    {
        private readonly ILogger _logger;

        public Catalog2Registration(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Catalog2Registration>();
        }

        public async Task Loop(string source, StorageFactory storageFactory, string contentBaseAddress, bool unlistShouldDelete, bool verbose, int interval, CancellationToken cancellationToken)
        {
            CommitCollector collector = new RegistrationCollector(new Uri(source), storageFactory, CommandHelpers.GetHttpMessageHandlerFactory(verbose))
            {
                ContentBaseAddress = contentBaseAddress == null
                    ? null
                    : new Uri(contentBaseAddress)
            };

            Storage storage = storageFactory.Create();
            ReadWriteCursor front = new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.MinValue);
            ReadCursor back = MemoryCursor.CreateMax();

            while (true)
            {
                bool run = false;
                do
                {
                    run = await collector.Run(front, back, cancellationToken);
                }
                while (run);

                Thread.Sleep(interval * 1000);
            }
        }

        public static void PrintUsage()
        {
            Console.WriteLine("Usage: ng catalog2registration "
                + $"-{Constants.Source} <catalog> "
                + $"-{Constants.ContentBaseAddress} <content-address> "
                + $"-{Constants.StorageBaseAddress} <storage-base-address> "
                + $"-{Constants.StorageType} file|azure "
                + $"[-{Constants.StoragePath} <path>]"
                + "|"
                + $"[-{Constants.StorageAccountName} <azure-acc>"
                    + $"-{Constants.StorageKeyValue} <azure-key> "
                    + $"-{Constants.StorageContainer} <azure-container> "
                    + $"-{Constants.StoragePath} <path> "
                    + $"[-{Constants.VaultName} <keyvault-name> "
                        + $"-{Constants.ClientId} <keyvault-client-id> "
                        + $"-{Constants.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                        + $"[-{Constants.ValidateCertificate} true|false]]] "
                + $"[-{Constants.Verbose} true|false] "
                + $"[-{Constants.Interval} <seconds>]");

            Console.WriteLine("To compress data in a separate container, add: "
                + $"-{Constants.UseCompressedStorage} [true|false] "
                + $"-{Constants.CompressedStorageBaseAddress} <storage-base-address> "
                + $"-{Constants.CompressedStorageAccountName} <azure-acc> "
                + $"-{Constants.CompressedStorageKeyValue} <azure-key> "
                + $"-{Constants.CompressedStorageContainer} <azure-container> "
                + $"-{Constants.CompressedStoragePath} <path>");
        }

        public void Run(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            string source = CommandHelpers.GetSource(arguments);
            bool unlistShouldDelete = CommandHelpers.GetUnlistShouldDelete(arguments, required: false);
            bool verbose = CommandHelpers.GetVerbose(arguments, required: false);
            int interval = CommandHelpers.GetInterval(arguments, defaultInterval: Constants.DefaultInterval);

            string contentBaseAddress = CommandHelpers.GetContentBaseAddress(arguments);

            StorageFactory storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            StorageFactory compressedStorageFactory = CommandHelpers.CreateCompressedStorageFactory(arguments, verbose);

            _logger.LogInformation("CONFIG source: \"{ConfigSource}\" storage: \"{Storage}\" interval: {Interval}", source, storageFactory, interval);

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

                Loop(source, aggregateStorageFactory, contentBaseAddress, unlistShouldDelete, verbose, interval, cancellationToken).Wait();
            }
            else
            {
                Loop(source, storageFactory, contentBaseAddress, unlistShouldDelete, verbose, interval, cancellationToken).Wait();
            }
        }
    }
}
