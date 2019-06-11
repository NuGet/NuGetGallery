// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng.Jobs
{
    public class Package2CatalogJob : NgJob
    {
        private string _gallery;
        private bool _verbose;
        private string _id;
        private string _version;
        private IStorage _storage;

        public Package2CatalogJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
        {
        }

        protected Package2CatalogJob(
            ITelemetryService telemetryService,
            ILoggerFactory loggerFactory,
            IStorage storage,
            string gallery,
            string packageId,
            string packageVersion,
            bool verbose)
            : this(telemetryService, loggerFactory)
        {
            _storage = storage;
            _gallery = gallery;
            _id = packageId;
            _version = packageVersion;
            _verbose = verbose;
        }

        public override string GetUsage()
        {
            return "Usage: ng package2catalog "
                   + $"-{Arguments.Gallery} <v2-feed-address> "
                   + $"-{Arguments.StorageBaseAddress} <storage-base-address> "
                   + $"-{Arguments.StorageType} file|azure "
                   + $"[-{Arguments.StoragePath} <path>]"
                   + "|"
                   + $"[-{Arguments.StorageAccountName} <azure-acc> "
                   + $"-{Arguments.StorageKeyValue} <azure-key> "
                   + $"-{Arguments.StorageContainer} <azure-container> "
                   + $"-{Arguments.StoragePath} <path>] "
                   + $"[-{Arguments.Verbose} true|false] "
                   + $"-{Arguments.Id} <id> "
                   + $"[-{Arguments.Version} <version>]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            _gallery = arguments.GetOrThrow<string>(Arguments.Gallery);
            _verbose = arguments.GetOrDefault(Arguments.Verbose, false);
            _id = arguments.GetOrThrow<string>(Arguments.Id);
            _version = arguments.GetOrDefault<string>(Arguments.Version);

            var storageFactory = CommandHelpers.CreateStorageFactory(arguments, _verbose);

            _storage = storageFactory.Create();
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            var timeout = TimeSpan.FromSeconds(300);

            using (var client = CreateHttpClient())
            {
                client.Timeout = timeout;

                //  if the version is specified a single package is processed otherwise all the packages corresponding to that id are processed

                var uri = (_version == null) ? MakePackageUri(_gallery, _id) : MakePackageUri(_gallery, _id, _version);

                var packages = await FeedHelpers.GetPackagesInOrder(client, uri, package => package.CreatedDate);

                Logger.LogInformation($"Downloading {packages.Select(t => t.Value.Count).Sum()} packages");

                //  the idea here is to leave the lastCreated, lastEdited and lastDeleted values exactly as they were
                var catalogProperties = await CatalogProperties.ReadAsync(_storage, TelemetryService, cancellationToken);
                var lastCreated = catalogProperties.LastCreated ?? DateTime.MinValue.ToUniversalTime();
                var lastEdited = catalogProperties.LastEdited ?? DateTime.MinValue.ToUniversalTime();
                var lastDeleted = catalogProperties.LastDeleted ?? DateTime.MinValue.ToUniversalTime();
                var packageCatalogItemCreator = PackageCatalogItemCreator.Create(
                    client,
                    TelemetryService,
                    Logger,
                    storage: null);

                await CatalogWriterHelper.WritePackageDetailsToCatalogAsync(
                    packageCatalogItemCreator,
                    packages,
                    _storage,
                    lastCreated,
                    lastEdited,
                    lastDeleted,
                    MaxDegreeOfParallelism,
                    createdPackages: null,
                    updateCreatedFromEdited: false,
                    cancellationToken: cancellationToken,
                    telemetryService: TelemetryService,
                    logger: Logger);
            }
        }

        protected virtual HttpClient CreateHttpClient()
        {
            return FeedHelpers.CreateHttpClient(CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, _verbose));
        }

        private static Uri MakePackageUri(string source, string id, string version)
        {
            var address =
                $"{source.Trim('/')}/Packages?$filter=Id%20eq%20'{id}'%20and%20Version%20eq%20'{version}'&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl";

            return new Uri(address);
        }

        private static Uri MakePackageUri(string source, string id)
        {
            var address =
                $"{source.Trim('/')}/Packages?$filter=Id%20eq%20'{id}'&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl";

            return new Uri(address);
        }
    }
}