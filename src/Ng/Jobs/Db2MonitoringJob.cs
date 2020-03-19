// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ng.Helpers;
using NuGet.Protocol;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Monitoring.Monitoring;
using NuGet.Services.Sql;
using NuGet.Services.Storage;

using CatalogStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.StorageFactory;
using CatalogStorage = NuGet.Services.Metadata.Catalog.Persistence.Storage;
using Constants = NuGet.Services.Metadata.Catalog.Constants;
using NuGet.Services.Logging;

namespace Ng.Jobs
{
    public class Db2MonitoringJob : LoopingNgJob
    {
        /// <summary>
        /// This job will continuously check packages within a range of <see cref="_monitoringCursor"/> minus 2 * <see cref="ReprocessRange"/> and <see cref="_monitoringCursor"/> minus 1 * <see cref="ReprocessRange"/>.
        /// </summary>
        private readonly static TimeSpan ReprocessRange = TimeSpan.FromHours(1);

        /// <remarks>
        /// Any values greater than <see cref="Constants.MaxPageSize"/> will be ignored by <see cref="DatabasePackageStatusOutdatedCheckSource"/>.
        /// </remarks>
        private const int Top = Constants.MaxPageSize;
        private const string GalleryCursorFileName = "gallerycursor.json";
        private const string DeletedCursorFileName = "deletedcursor.json";

        private const int DefaultMaxQueueSize = 100;
        private int _maxRequeueQueueSize;

        private IGalleryDatabaseQueryService _galleryDatabaseQueryService;
        private IStorageQueue<PackageValidatorContext> _packageValidatorContextQueue;
        private IPackageMonitoringStatusService _statusService;
        private ReadCursor _monitoringCursor;
        private ReadWriteCursor _galleryCursor;

        private CatalogStorage _auditingStorage;
        private ReadWriteCursor _deletedCursor;

        private CollectorHttpClient _client;

        public Db2MonitoringJob(
            ILoggerFactory loggerFactory,
            ITelemetryClient telemetryClient,
            IDictionary<string, string> telemetryGlobalDimensions)
            : base(loggerFactory, telemetryClient, telemetryGlobalDimensions)
        {
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);
            _maxRequeueQueueSize = arguments.GetOrDefault(Arguments.MaxRequeueQueueSize, DefaultMaxQueueSize);

            CommandHelpers.AssertAzureStorage(arguments);

            var monitoringStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            _statusService = CommandHelpers.GetPackageMonitoringStatusService(arguments, monitoringStorageFactory, LoggerFactory);
            _packageValidatorContextQueue = CommandHelpers.CreateStorageQueue<PackageValidatorContext>(arguments, PackageValidatorContext.Version);

            Logger.LogInformation(
                "CONFIG storage: {Storage}",
                monitoringStorageFactory);

            _monitoringCursor = ValidationFactory.GetFront(monitoringStorageFactory);
            _galleryCursor = CreateCursor(monitoringStorageFactory, GalleryCursorFileName);
            _deletedCursor = CreateCursor(monitoringStorageFactory, DeletedCursorFileName);

            var connectionString = arguments.GetOrThrow<string>(Arguments.ConnectionString);
            var galleryDbConnection = new AzureSqlConnectionFactory(
                connectionString,
                SecretInjector,
                LoggerFactory.CreateLogger<AzureSqlConnectionFactory>());

            var packageContentUriBuilder = new PackageContentUriBuilder(
                arguments.GetOrThrow<string>(Arguments.PackageContentUrlFormat));

            var timeoutInSeconds = arguments.GetOrDefault(Arguments.SqlCommandTimeoutInSeconds, 300);
            _galleryDatabaseQueryService = new GalleryDatabaseQueryService(
                galleryDbConnection,
                packageContentUriBuilder,
                TelemetryService,
                timeoutInSeconds);

            var auditingStorageFactory = CommandHelpers.CreateSuffixedStorageFactory(
                "Auditing",
                arguments,
                verbose,
                new SemaphoreSlimThrottle(new SemaphoreSlim(ServicePointManager.DefaultConnectionLimit)));

            _auditingStorage = auditingStorageFactory.Create();

            var messageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, verbose);
            _client = new CollectorHttpClient(messageHandlerFactory());
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            var databaseSource = new DatabasePackageStatusOutdatedCheckSource(
                _galleryCursor, _galleryDatabaseQueryService);

            var auditingSource = new AuditingStoragePackageStatusOutdatedCheckSource(
                _deletedCursor, _auditingStorage, LoggerFactory.CreateLogger<AuditingStoragePackageStatusOutdatedCheckSource>());

            var sources = new IPackageStatusOutdatedCheckSource[] { databaseSource, auditingSource };

            var hasPackagesToProcess = true;
            while (hasPackagesToProcess)
            {
                var currentMessageCount = await _packageValidatorContextQueue.GetMessageCount(cancellationToken);
                if (currentMessageCount > _maxRequeueQueueSize)
                {
                    Logger.LogInformation(
                        "Can't continue processing packages because the queue has too many messages ({CurrentMessageCount} > {MaxRequeueQueueSize})!",
                        currentMessageCount, _maxRequeueQueueSize);
                    return;
                }

                hasPackagesToProcess = await CheckPackages(
                    sources,
                    cancellationToken);
            }

            Logger.LogInformation("All packages have had their status checked.");
            await _monitoringCursor.LoadAsync(cancellationToken);
            var newCursorValue = _monitoringCursor.Value - ReprocessRange - ReprocessRange;
            Logger.LogInformation("Restarting source cursors to {NewCursorValue}.", newCursorValue);
            foreach (var source in sources)
            {
                await source.MoveBackAsync(newCursorValue, cancellationToken);
            }
        }

        private async Task<bool> CheckPackages(
            IReadOnlyCollection<IPackageStatusOutdatedCheckSource> sources,
            CancellationToken cancellationToken)
        {
            Logger.LogInformation("Fetching packages to check status of.");
            var packagesToCheck = new List<PackageStatusOutdatedCheck>();
            await _monitoringCursor.LoadAsync(cancellationToken);
            foreach (var source in sources)
            {
                packagesToCheck.AddRange(await source.GetPackagesToCheckAsync(
                    _monitoringCursor.Value - ReprocessRange, Top, cancellationToken));
            }

            var packagesToCheckBag = new ConcurrentBag<PackageStatusOutdatedCheck>(packagesToCheck);

            Logger.LogInformation("Found {PackagesToCheckCount} packages to check status of.", packagesToCheck.Count());
            await ParallelAsync.Repeat(() => ProcessPackagesAsync(packagesToCheckBag, cancellationToken));
            Logger.LogInformation("Finished checking status of packages.");

            foreach (var source in sources)
            {
                await source.MarkPackagesCheckedAsync(cancellationToken);
            }

            return packagesToCheck.Any();
        }

        private async Task ProcessPackagesAsync(
            ConcurrentBag<PackageStatusOutdatedCheck> checkBag, CancellationToken cancellationToken)
        {
            while (checkBag.TryTake(out var check))
            {
                if (await IsStatusOutdatedAsync(check, cancellationToken))
                {
                    Logger.LogWarning("Status for {Id} {Version} is outdated!", check.Identity.Id, check.Identity.Version);
                    var context = new PackageValidatorContext(check.Identity, null);
                    await _packageValidatorContextQueue.AddAsync(context, cancellationToken);
                }
                else
                {
                    Logger.LogInformation("Status for {Id} {Version} is up to date.", check.Identity.Id, check.Identity.Version);
                }
            }
        }

        private async Task<bool> IsStatusOutdatedAsync(
            PackageStatusOutdatedCheck check, CancellationToken cancellationToken)
        {
            var status = await _statusService.GetAsync(check.Identity, cancellationToken);

            var catalogEntries = status?.ValidationResult?.CatalogEntries;
            if (catalogEntries == null || !catalogEntries.Any())
            {
                return true;
            }

            var latestCatalogEntryTimestampMetadata = await PackageTimestampMetadata.FromCatalogEntries(_client, catalogEntries);
            return check.Timestamp > latestCatalogEntryTimestampMetadata.Last;
        }

        private DurableCursor CreateCursor(CatalogStorageFactory storageFactory, string filename)
        {
            var storage = storageFactory.Create();
            return new DurableCursor(storage.ResolveUri(filename), storage, MemoryCursor.MinValue);
        }
    }
}
