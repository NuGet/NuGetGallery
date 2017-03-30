// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;

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
                   + $"-{Arguments.CompressedStoragePath} <path>"
                   + Environment.NewLine
                   + "To generate registration blobs that contain SemVer 2.0.0 packages, add: "
                   + $"-{Arguments.UseSemVer2Storage} [true|false] "
                   + $"-{Arguments.SemVer2StorageBaseAddress} <storage-base-address> "
                   + $"-{Arguments.SemVer2StorageAccountName} <azure-acc> "
                   + $"-{Arguments.SemVer2StorageKeyValue} <azure-key> "
                   + $"-{Arguments.SemVer2StorageContainer} <azure-container> "
                   + $"-{Arguments.SemVer2StoragePath} <path>";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var unlistShouldDelete = arguments.GetOrDefault(Arguments.UnlistShouldDelete, false);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            var contentBaseAddress = arguments.GetOrDefault<string>(Arguments.ContentBaseAddress);

            // The term "legacy" here refers to the registration hives that do not contain any SemVer 2.0.0 packages.
            // In production, this is two registration hives:
            //   1) the first hive released, which is not gzipped and does not have SemVer 2.0.0 packages
            //   2) the secondary hive released, which is gzipped but does not have SemVer 2.0.0 packages
            StorageFactory legacyStorageFactory;

            var storageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            var compressedStorageFactory = CommandHelpers.CreateCompressedStorageFactory(arguments, verbose);
            var semVer2StorageFactory = CommandHelpers.CreateSemVer2StorageFactory(arguments, verbose);

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

                legacyStorageFactory = aggregateStorageFactory;
            }
            else
            {
                legacyStorageFactory = storageFactory;
            }

            _collector = new RegistrationCollector(
                new Uri(source),
                legacyStorageFactory,
                semVer2StorageFactory,
                CommandHelpers.GetHttpMessageHandlerFactory(verbose))
            {
                ContentBaseAddress = contentBaseAddress == null
                    ? null
                    : new Uri(contentBaseAddress)
            };

            var cursorStorage = legacyStorageFactory.Create();
            _front = new DurableCursor(cursorStorage.ResolveUri("cursor.json"), cursorStorage, MemoryCursor.MinValue);
            _back = MemoryCursor.CreateMax();

            semVer2StorageFactory?.Create();
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
