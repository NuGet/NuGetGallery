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

namespace Ng.Jobs
{
    public class Catalog2RegistrationJob : LoopingNgJob
    {
        private CommitCollector _collector;
        private ReadWriteCursor _front;
        private ReadCursor _back;
        private Uri _destination;

        public Catalog2RegistrationJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng catalog2registration "
                   + $"-{Arguments.Source} <catalog> "
                   + $"-{Arguments.ContentBaseAddress} <content-address> "
                   + $"-{Arguments.GalleryBaseAddress} <gallery-base-address> "
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
                   + $"-{Arguments.SemVer2StoragePath} <path>"
                   + Environment.NewLine
                   + "Optional Arguments"
                   + $"-{Arguments.StorageOperationMaxExecutionTimeInSeconds} <azureOperationMaxExecutionTimeInSeconds>"
                   + $"-{Arguments.StorageServerTimeoutInSeconds} <storageServerTimeoutInSeconds>"
                   + $"-{Arguments.AllIconsInFlatContainer} [true|false]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            var contentBaseAddress = arguments.GetOrDefault<string>(Arguments.ContentBaseAddress);
            var galleryBaseAddress = arguments.GetOrDefault<string>(Arguments.GalleryBaseAddress);
            var isContentFlatContainer = arguments.GetOrDefault<bool>(Arguments.ContentIsFlatContainer);
            var allIconsInFlatContainer = arguments.GetOrDefault<bool>(Arguments.AllIconsInFlatContainer);
            var maxConcurrentBatches = MaxConcurrentBatches(arguments.GetOrDefault<int>(Arguments.MaxConcurrentBatches));

            // The term "legacy" here refers to the registration hives that do not contain any SemVer 2.0.0 packages.
            // In production, this is two registration hives:
            //   1) the first hive released, which is not gzipped and does not have SemVer 2.0.0 packages
            //   2) the secondary hive released, which is gzipped but does not have SemVer 2.0.0 packages
            var storageFactories = CommandHelpers.CreateRegistrationStorageFactories(arguments, verbose);

            Logger.LogInformation(
                "CONFIG source: \"{ConfigSource}\" storage: \"{Storage}\"",
                source,
                storageFactories.LegacyStorageFactory);

            if (isContentFlatContainer)
            {
                var flatContainerCursorUriString = arguments.GetOrThrow<string>(Arguments.CursorUri);
                var flatContainerName = arguments.GetOrThrow<string>(Arguments.FlatContainerName);
                RegistrationMakerCatalogItem.PackagePathProvider = new FlatContainerPackagePathProvider(flatContainerName);
                // In case that the flat container is used as the packages' source the registration needs to wait for the flatcontainer cursor
                _back = new HttpReadCursor(new Uri(flatContainerCursorUriString));
            }
            else
            {
                RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();
                _back = MemoryCursor.CreateMax();
            }

            _collector = new RegistrationCollector(
                new Uri(source),
                storageFactories.LegacyStorageFactory,
                storageFactories.SemVer2StorageFactory,
                contentBaseAddress == null ? null : new Uri(contentBaseAddress),
                galleryBaseAddress == null ? null : new Uri(galleryBaseAddress),
                allIconsInFlatContainer,
                TelemetryService,
                Logger,
                CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, verbose),
                maxConcurrentBatches: maxConcurrentBatches);

            var cursorStorage = storageFactories.LegacyStorageFactory.Create();
            _front = new DurableCursor(cursorStorage.ResolveUri("cursor.json"), cursorStorage, MemoryCursor.MinValue);
            storageFactories.SemVer2StorageFactory?.Create();

            _destination = storageFactories.LegacyStorageFactory.DestinationAddress;
            TelemetryService.GlobalDimensions[TelemetryConstants.Destination] = _destination?.AbsoluteUri;
        }

        private int MaxConcurrentBatches(int argBatches)
        {
            if (argBatches < 0)
            {
                throw new ArgumentException($"{nameof(argBatches)} cannot be negative.");
            }
            if (argBatches == 0)
            {
                return RegistrationCollector.DefaultMaxConcurrentBatches;
            }

            return argBatches;
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            using (Logger.BeginScope($"Logging for {{{TelemetryConstants.Destination}}}", _destination?.AbsoluteUri))
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