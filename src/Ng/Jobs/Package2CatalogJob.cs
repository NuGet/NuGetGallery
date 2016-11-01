// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace Ng.Jobs
{
    public class Package2CatalogJob : NgJob
    {
        private string _gallery;
        private bool _verbose;
        private string _id;
        private string _version;
        private StorageFactory _storageFactory;

        public Package2CatalogJob(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
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
            _storageFactory = CommandHelpers.CreateStorageFactory(arguments, _verbose);
        }

        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            var timeout = TimeSpan.FromSeconds(300);

            using (var client = CatalogUtility.CreateHttpClient(_verbose))
            {
                client.Timeout = timeout;

                //  if the version is specified a single package is processed otherwise all the packages corresponding to that id are processed

                var uri = (_version == null) ? MakePackageUri(_gallery, _id) : MakePackageUri(_gallery, _id, _version);

                var packages = await CatalogUtility.GetPackages(client, uri, "Created");

                Logger.LogInformation($"Downloading {packages.Select(t => t.Value.Count).Sum()} packages");

                var storage = _storageFactory.Create();

                //  the idea here is to leave the lastCreated, lastEdited and lastDeleted values exactly as they were
                var lastCreated = await CatalogUtility.GetCatalogProperty(storage, "nuget:lastCreated", cancellationToken) ?? DateTime.MinValue.ToUniversalTime();
                var lastEdited = await CatalogUtility.GetCatalogProperty(storage, "nuget:lastEdited", cancellationToken) ?? DateTime.MinValue.ToUniversalTime();
                var lastDeleted = await CatalogUtility.GetCatalogProperty(storage, "nuget:lastDeleted", cancellationToken) ?? DateTime.MinValue.ToUniversalTime();

                await CatalogUtility.DownloadMetadata2Catalog(client, packages, storage, lastCreated, lastEdited, lastDeleted, null, cancellationToken, Logger);
            }
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
