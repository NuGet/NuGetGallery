// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ng.Helpers;
using NuGet.Protocol;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Sql;
using Constants = NuGet.Services.Metadata.Catalog.Constants;

namespace Ng.Jobs
{
    public class Db2CatalogJob : LoopingNgJob
    {
        protected bool Verbose;
        protected ISqlConnectionFactory GalleryDbConnection;
        protected PackageContentUriBuilder PackageContentUriBuilder;
        protected IGalleryDatabaseQueryService GalleryDatabaseQueryService;
        protected IStorage CatalogStorage;
        protected IStorage AuditingStorage;
        protected IStorage PreferredPackageSourceStorage;
        protected DateTime? StartDate;
        protected TimeSpan Timeout;
        protected int Top;
        protected Uri Destination;
        protected bool SkipCreatedPackagesProcessing;

        public Db2CatalogJob(
            ILoggerFactory loggerFactory,
            ITelemetryClient telemetryClient,
            IDictionary<string, string> telemetryGlobalDimensions)
            : base(loggerFactory, telemetryClient, telemetryGlobalDimensions)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng db2catalog "
                   + $"-{Arguments.ConnectionString} <gallery-db-connectionstring> "
                   + $"-{Arguments.CursorSize} <cursor-size> "
                   + $"-{Arguments.PackageContentUrlFormat} <package-content-url-format> "
                   + $"-{Arguments.StorageBaseAddress} <storage-base-address> "
                   + $"-{Arguments.StorageType} file|azure "
                   + $"[-{Arguments.StoragePath} <path>]"
                   + "|"
                   + $"[-{Arguments.StorageAccountName} <azure-acc> "
                   + $"-{Arguments.StorageKeyValue} <azure-key> "
                   + $"-{Arguments.StorageContainer} <azure-container> "
                   + $"-{Arguments.StoragePath} <path> "
                   + $"[-{Arguments.VaultName} <keyvault-name> "
                   + $"-{Arguments.UseManagedIdentity} true|false "
                   + $"-{Arguments.ClientId} <keyvault-client-id> Should not be set if {Arguments.UseManagedIdentity} is true"
                   + $"-{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> Should not be set if {Arguments.UseManagedIdentity} is true"
                   + $"[-{Arguments.ValidateCertificate} true|false]]] "
                   + $"-{Arguments.StorageTypeAuditing} file|azure "
                   + $"[-{Arguments.StoragePathAuditing} <path>]"
                   + "|"
                   + $"[-{Arguments.StorageAccountNameAuditing} <azure-acc> "
                   + $"-{Arguments.StorageKeyValueAuditing} <azure-key> "
                   + $"-{Arguments.StorageContainerAuditing} <azure-container> "
                   + $"-{Arguments.StoragePathAuditing} <path>] "
                   + "|"
                   + $"[-{Arguments.PreferAlternatePackageSourceStorage} true|false "
                   + $"-{Arguments.StorageAccountNamePreferredPackageSourceStorage} <azure-acc> "
                   + $"-{Arguments.StorageKeyValuePreferredPackageSourceStorage} <azure-key> "
                   + $"-{Arguments.StorageContainerPreferredPackageSourceStorage} <azure-container>] "
                   + $"[-{Arguments.SkipCreatedPackagesProcessing} true|false] "
                   + $"[-{Arguments.Verbose} true|false] "
                   + $"[-{Arguments.Interval} <seconds>] "
                   + $"[-{Arguments.StartDate} <DateTime>]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            Verbose = arguments.GetOrDefault(Arguments.Verbose, false);
            StartDate = arguments.GetOrDefault(Arguments.StartDate, Constants.DateTimeMinValueUtc);
            Top = arguments.GetOrDefault(Arguments.CursorSize, 20);
            SkipCreatedPackagesProcessing = arguments.GetOrDefault(Arguments.SkipCreatedPackagesProcessing, false);

            StorageFactory preferredPackageSourceStorageFactory = null;

            var preferAlternatePackageSourceStorage = arguments.GetOrDefault(Arguments.PreferAlternatePackageSourceStorage, false);

            if (preferAlternatePackageSourceStorage)
            {
                preferredPackageSourceStorageFactory = CommandHelpers.CreateSuffixedStorageFactory(
                    "PreferredPackageSourceStorage",
                    arguments,
                    Verbose,
                    new SemaphoreSlimThrottle(new SemaphoreSlim(ServicePointManager.DefaultConnectionLimit)));
            }

            var catalogStorageFactory = CommandHelpers.CreateStorageFactory(
                arguments,
                Verbose,
                new SemaphoreSlimThrottle(new SemaphoreSlim(ServicePointManager.DefaultConnectionLimit)));

            var auditingStorageFactory = CommandHelpers.CreateSuffixedStorageFactory(
                "Auditing",
                arguments,
                Verbose,
                new SemaphoreSlimThrottle(new SemaphoreSlim(ServicePointManager.DefaultConnectionLimit)));

            Logger.LogInformation("CONFIG source: \"{ConfigSource}\" storage: \"{Storage}\" preferred package source storage: \"{PreferredPackageSourceStorage}\"",
                GalleryDbConnection,
                catalogStorageFactory,
                preferredPackageSourceStorageFactory);

            CatalogStorage = catalogStorageFactory.Create();
            AuditingStorage = auditingStorageFactory.Create();

            if (preferAlternatePackageSourceStorage)
            {
                PreferredPackageSourceStorage = preferredPackageSourceStorageFactory.Create();
            }

            Destination = catalogStorageFactory.BaseAddress;
            TelemetryService.GlobalDimensions[TelemetryConstants.Destination] = Destination.AbsoluteUri;

            // Setup gallery database access
            PackageContentUriBuilder = new PackageContentUriBuilder(
                arguments.GetOrThrow<string>(Arguments.PackageContentUrlFormat));

            var connectionString = arguments.GetOrThrow<string>(Arguments.ConnectionString);
            GalleryDbConnection = new AzureSqlConnectionFactory(
                connectionString,
                new EmptySecretInjector(),
                LoggerFactory.CreateLogger<AzureSqlConnectionFactory>());

            var timeoutInSeconds = arguments.GetOrDefault(Arguments.SqlCommandTimeoutInSeconds, 300);
            Timeout = TimeSpan.FromSeconds(timeoutInSeconds);
            GalleryDatabaseQueryService = new GalleryDatabaseQueryService(
                GalleryDbConnection,
                PackageContentUriBuilder,
                TelemetryService,
                timeoutInSeconds);
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            using (Logger.BeginScope($"Logging for {{{TelemetryConstants.Destination}}}", Destination.AbsoluteUri))
            using (TelemetryService.TrackDuration(TelemetryConstants.JobLoopSeconds))
            using (var client = CreateHttpClient())
            {
                uint packagesDeleted;
                uint packagesCreated;
                uint packagesEdited;

                client.Timeout = Timeout;

                var packageCatalogItemCreator = PackageCatalogItemCreator.Create(
                    client,
                    TelemetryService,
                    Logger,
                    PreferredPackageSourceStorage);

                do
                {
                    packagesDeleted = 0;
                    packagesCreated = 0;
                    packagesEdited = 0;

                    // baseline timestamps
                    var catalogProperties = await CatalogProperties.ReadAsync(CatalogStorage, TelemetryService, cancellationToken);
                    var lastCreated = catalogProperties.LastCreated ?? (StartDate ?? Constants.DateTimeMinValueUtc);
                    var lastEdited = catalogProperties.LastEdited ?? lastCreated;
                    var lastDeleted = catalogProperties.LastDeleted ?? lastCreated;

                    if (lastDeleted == Constants.DateTimeMinValueUtc)
                    {
                        lastDeleted = SkipCreatedPackagesProcessing ? lastEdited : lastCreated;
                    }

                    try
                    {
                        if (lastDeleted > Constants.DateTimeMinValueUtc)
                        {
                            using (TelemetryService.TrackDuration(TelemetryConstants.DeletedPackagesSeconds))
                            {
                                Logger.LogInformation("CATALOG LastDeleted: {CatalogDeletedTime}", lastDeleted.ToString("O"));

                                var deletedPackages = await GetDeletedPackages(AuditingStorage, lastDeleted);

                                packagesDeleted = (uint)deletedPackages.SelectMany(x => x.Value).Count();
                                Logger.LogInformation("FEED DeletedPackages: {DeletedPackagesCount}", packagesDeleted);

                                // We want to ensure a commit only contains each package once at most.
                                // Therefore we segment by package id + version.
                                var deletedPackagesSegments = SegmentPackageDeletes(deletedPackages);
                                foreach (var deletedPackagesSegment in deletedPackagesSegments)
                                {
                                    lastDeleted = await Deletes2Catalog(
                                        deletedPackagesSegment, CatalogStorage, lastCreated, lastEdited, lastDeleted, cancellationToken);

                                    // Wait for one second to ensure the next catalog commit gets a new timestamp
                                    Thread.Sleep(TimeSpan.FromSeconds(1));
                                }
                            }
                        }

                        if (!SkipCreatedPackagesProcessing)
                        {
                            using (TelemetryService.TrackDuration(TelemetryConstants.CreatedPackagesSeconds))
                            {
                                Logger.LogInformation("CATALOG LastCreated: {CatalogLastCreatedTime}", lastCreated.ToString("O"));

                                var createdPackages = await GalleryDatabaseQueryService.GetPackagesCreatedSince(lastCreated, Top);

                                packagesCreated = (uint)createdPackages.SelectMany(x => x.Value).Count();
                                Logger.LogInformation("DATABASE CreatedPackages: {CreatedPackagesCount}", packagesCreated);

                                lastCreated = await CatalogWriterHelper.WritePackageDetailsToCatalogAsync(
                                    packageCatalogItemCreator,
                                    createdPackages,
                                    CatalogStorage,
                                    lastCreated,
                                    lastEdited,
                                    lastDeleted,
                                    MaxDegreeOfParallelism,
                                    createdPackages: true,
                                    updateCreatedFromEdited: false,
                                    cancellationToken: cancellationToken,
                                    telemetryService: TelemetryService,
                                    logger: Logger);
                            }
                        }

                        using (TelemetryService.TrackDuration(TelemetryConstants.EditedPackagesSeconds))
                        {
                            Logger.LogInformation("CATALOG LastEdited: {CatalogLastEditedTime}", lastEdited.ToString("O"));

                            var editedPackages = await GalleryDatabaseQueryService.GetPackagesEditedSince(lastEdited, Top);

                            packagesEdited = (uint)editedPackages.SelectMany(x => x.Value).Count();
                            Logger.LogInformation("DATABASE EditedPackages: {EditedPackagesCount}", packagesEdited);

                            lastEdited = await CatalogWriterHelper.WritePackageDetailsToCatalogAsync(
                                packageCatalogItemCreator,
                                editedPackages,
                                CatalogStorage,
                                lastCreated,
                                lastEdited,
                                lastDeleted,
                                MaxDegreeOfParallelism,
                                createdPackages: false,
                                updateCreatedFromEdited: SkipCreatedPackagesProcessing,
                                cancellationToken: cancellationToken,
                                telemetryService: TelemetryService,
                                logger: Logger);
                        }
                    }
                    finally
                    {
                        TelemetryService.TrackMetric(TelemetryConstants.DeletedPackagesCount, packagesDeleted);

                        if (!SkipCreatedPackagesProcessing)
                        {
                            TelemetryService.TrackMetric(TelemetryConstants.CreatedPackagesCount, packagesCreated);
                        }

                        TelemetryService.TrackMetric(TelemetryConstants.EditedPackagesCount, packagesEdited);
                    }
                } while (packagesDeleted > 0 || packagesCreated > 0 || packagesEdited > 0);
            }
        }

        // Wrapper function for CatalogUtility.CreateHttpClient
        // Overriden by NgTests.TestableDb2CatalogJob
        protected virtual HttpClient CreateHttpClient()
        {
            return FeedHelpers.CreateHttpClient(CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, Verbose));
        }

        private async Task<SortedList<DateTime, IList<FeedPackageIdentity>>> GetDeletedPackages(IStorage auditingStorage, DateTime since)
        {
            var result = new SortedList<DateTime, IList<FeedPackageIdentity>>();

            // Get all audit blobs (based on their filename which starts with a date that can be parsed)
            // NOTE we're getting more files than needed (to account for a time difference between servers)
            var minimumFileTime = since.AddMinutes(-15);
            var auditEntries = await DeletionAuditEntry.GetAsync(auditingStorage, CancellationToken.None,
                minTime: minimumFileTime, logger: Logger);

            foreach (var auditEntry in auditEntries)
            {
                if (!string.IsNullOrEmpty(auditEntry.PackageId) && !string.IsNullOrEmpty(auditEntry.PackageVersion) && auditEntry.TimestampUtc > since)
                {
                    // Mark the package "deleted"
                    if (!result.TryGetValue(auditEntry.TimestampUtc.Value, out var packages))
                    {
                        packages = new List<FeedPackageIdentity>();
                        result.Add(auditEntry.TimestampUtc.Value, packages);
                    }

                    packages.Add(new FeedPackageIdentity(auditEntry.PackageId, auditEntry.PackageVersion));
                }
            }

            return result;
        }

        private static IEnumerable<SortedList<DateTime, IList<FeedPackageIdentity>>> SegmentPackageDeletes(SortedList<DateTime, IList<FeedPackageIdentity>> packageDeletes)
        {
            var packageIdentityTracker = new HashSet<string>();
            var currentSegment = new SortedList<DateTime, IList<FeedPackageIdentity>>();
            foreach (var entry in packageDeletes)
            {
                if (!currentSegment.ContainsKey(entry.Key))
                {
                    currentSegment.Add(entry.Key, new List<FeedPackageIdentity>());
                }

                var curentSegmentPackages = currentSegment[entry.Key];
                foreach (var packageIdentity in entry.Value)
                {
                    var key = packageIdentity.Id + "|" + packageIdentity.Version;
                    if (packageIdentityTracker.Contains(key))
                    {
                        // Duplicate, return segment
                        yield return currentSegment;

                        // Clear current segment
                        currentSegment.Clear();
                        currentSegment.Add(entry.Key, new List<FeedPackageIdentity>());
                        curentSegmentPackages = currentSegment[entry.Key];
                        packageIdentityTracker.Clear();
                    }

                    // Add to segment
                    curentSegmentPackages.Add(packageIdentity);
                    packageIdentityTracker.Add(key);
                }
            }

            if (currentSegment.Any())
            {
                yield return currentSegment;
            }
        }

        private async Task<DateTime> Deletes2Catalog(
            SortedList<DateTime, IList<FeedPackageIdentity>> packages,
            IStorage storage,
            DateTime lastCreated,
            DateTime lastEdited,
            DateTime lastDeleted,
            CancellationToken cancellationToken)
        {
            var writer = new AppendOnlyCatalogWriter(
                storage,
                TelemetryService,
                Constants.MaxPageSize);

            if (packages == null || packages.Count == 0)
            {
                return lastDeleted;
            }

            foreach (var entry in packages)
            {
                foreach (var packageIdentity in entry.Value)
                {
                    var catalogItem = new DeleteCatalogItem(packageIdentity.Id, packageIdentity.Version, entry.Key);
                    writer.Add(catalogItem);

                    Logger.LogInformation("Delete: {PackageId} {PackageVersion}", packageIdentity.Id, packageIdentity.Version);
                }

                lastDeleted = entry.Key;
            }

            var commitMetadata = PackageCatalog.CreateCommitMetadata(writer.RootUri, new CommitMetadata(lastCreated, lastEdited, lastDeleted));

            await writer.Commit(commitMetadata, cancellationToken);

            Logger.LogInformation("COMMIT package deletes to catalog.");

            return lastDeleted;
        }
    }
}