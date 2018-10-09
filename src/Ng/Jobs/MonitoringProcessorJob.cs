// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Storage;
using NuGet.Versioning;

namespace Ng.Jobs
{
    /// <summary>
    /// Runs validations on packages from a <see cref="IStorageQueue{PackageValidatorContext}"/> using a <see cref="PackageValidator"/>.
    /// </summary>
    public class MonitoringProcessorJob : LoopingNgJob
    {
        private PackageValidator _packageValidator;
        private IStorageQueue<PackageValidatorContext> _queue;
        private IPackageMonitoringStatusService _statusService;
        private IMonitoringNotificationService _notificationService;
        private RegistrationResourceV3 _regResource;
        private NuGet.Common.ILogger CommonLogger;
        private CollectorHttpClient _client;

        public MonitoringProcessorJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
        {
            CommonLogger = Logger.AsCommon();
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var gallery = arguments.GetOrThrow<string>(Arguments.Gallery);
            var index = arguments.GetOrThrow<string>(Arguments.Index);
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var requireSignature = arguments.GetOrDefault(Arguments.RequireSignature, false);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            CommandHelpers.AssertAzureStorage(arguments);

            var monitoringStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            var auditingStorageFactory = CommandHelpers.CreateSuffixedStorageFactory("Auditing", arguments, verbose);

            var endpointInputs = CommandHelpers.GetEndpointFactoryInputs(arguments);
            var messageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, verbose);

            Logger.LogInformation(
                "CONFIG gallery: {Gallery} index: {Index} storage: {Storage} auditingStorage: {AuditingStorage} endpoints: {Endpoints}",
                gallery, index, monitoringStorageFactory, auditingStorageFactory, string.Join(", ", endpointInputs.Select(e => e.Name)));

            _packageValidator = new PackageValidatorFactory(LoggerFactory)
                .Create(gallery, index, auditingStorageFactory, endpointInputs, messageHandlerFactory, requireSignature, verbose);

            _queue = CommandHelpers.CreateStorageQueue<PackageValidatorContext>(arguments, PackageValidatorContext.Version);

            _statusService = CommandHelpers.GetPackageMonitoringStatusService(arguments, monitoringStorageFactory, LoggerFactory);

            _notificationService = new LoggerMonitoringNotificationService(LoggerFactory.CreateLogger<LoggerMonitoringNotificationService>());

            _regResource = Repository.Factory.GetCoreV3(index).GetResource<RegistrationResourceV3>(cancellationToken);

            _client = new CollectorHttpClient(messageHandlerFactory());
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            await ParallelAsync.Repeat(() => ProcessPackagesAsync(cancellationToken));
        }

        private async Task ProcessPackagesAsync(CancellationToken token)
        {
            StorageQueueMessage<PackageValidatorContext> queueMessage = null;
            do
            {
                Logger.LogInformation("Fetching next queue message.");
                queueMessage = await _queue.GetNextAsync(token);
                await HandleQueueMessageAsync(queueMessage, token);
            } while (queueMessage != null);

            Logger.LogInformation("No messages left in queue.");
        }

        private async Task HandleQueueMessageAsync(
            StorageQueueMessage<PackageValidatorContext> queueMessage,
            CancellationToken token)
        {
            if (queueMessage == null)
            {
                return;
            }

            var queuedContext = queueMessage.Contents;
            var messageWasProcessed = false;

            try
            {
                await RunPackageValidatorAsync(queuedContext, token);
                // The validations ran successfully and were saved to storage.
                // We can remove the message from the queue because it was processed.
                messageWasProcessed = true;
            }
            catch (Exception e)
            {
                // Validations failed to run! Save this failed status to storage.
                await SaveFailedPackageMonitoringStatusAsync(queuedContext, e, token);
                // We can then remove the message from the queue because this failed status can be used to requeue the message.
                messageWasProcessed = true;
            }

            // Note that if both validations fail and saving the failure status fail, we cannot remove the message from the queue.
            if (messageWasProcessed)
            {
                await _queue.RemoveAsync(queueMessage, token);
            }
        }

        private async Task RunPackageValidatorAsync(
            PackageValidatorContext queuedContext,
            CancellationToken token)
        {
            var feedPackage = queuedContext.Package;
            Logger.LogInformation("Running PackageValidator on PackageValidatorContext for {PackageId} {PackageVersion}.", feedPackage.Id, feedPackage.Version);
            IEnumerable<CatalogIndexEntry> catalogEntries = null;

            if (queuedContext.CatalogEntries != null)
            {
                catalogEntries = queuedContext.CatalogEntries;
            }
            else
            {
                Logger.LogInformation("PackageValidatorContext for {PackageId} {PackageVersion} is missing catalog entries! " +
                    "Attempting to fetch most recent catalog entry from registration.",
                    feedPackage.Id, feedPackage.Version);

                catalogEntries = await FetchCatalogIndexEntriesFromRegistrationAsync(feedPackage, token);
            }

            var existingStatus = await _statusService.GetAsync(feedPackage, token);

            if (existingStatus?.ValidationResult != null && CompareCatalogEntries(catalogEntries, existingStatus.ValidationResult.CatalogEntries))
            {
                // A newer catalog entry of this package has already been validated.
                Logger.LogInformation("A newer catalog entry of {PackageId} {PackageVersion} has already been processed ({OldCommitTimeStamp} < {NewCommitTimeStamp}).",
                    feedPackage.Id, feedPackage.Version,
                    catalogEntries.Max(c => c.CommitTimeStamp),
                    existingStatus.ValidationResult.CatalogEntries.Max(c => c.CommitTimeStamp));

                return;
            }

            var context = new PackageValidatorContext(feedPackage, catalogEntries);

            var result = await _packageValidator.ValidateAsync(context, _client, token);

            await _notificationService.OnPackageValidationFinishedAsync(result, token);

            var status = new PackageMonitoringStatus(result);
            await _statusService.UpdateAsync(status, token);
        }

        private async Task<IEnumerable<CatalogIndexEntry>> FetchCatalogIndexEntriesFromRegistrationAsync(
            FeedPackageIdentity feedPackage,
            CancellationToken token)
        {
            var id = feedPackage.Id;
            var version = NuGetVersion.Parse(feedPackage.Version);
            var leafBlob = await _regResource.GetPackageMetadata(
                new PackageIdentity(id, version),
                NullSourceCacheContext.Instance,
                Logger.AsCommon(),
                token);

            if (leafBlob == null)
            {
                throw new Exception("Package is missing from registration!");
            }

            var catalogPageUri = new Uri(leafBlob["@id"].ToString());
            var catalogPage = await _client.GetJObjectAsync(catalogPageUri, token);
            return new CatalogIndexEntry[]
            {
                new CatalogIndexEntry(
                    catalogPageUri,
                    Schema.DataTypes.PackageDetails.ToString(),
                    catalogPage["catalog:commitId"].ToString(),
                    DateTime.Parse(catalogPage["catalog:commitTimeStamp"].ToString()),
                    id,
                    version)
            };
        }

        private async Task SaveFailedPackageMonitoringStatusAsync(
            PackageValidatorContext queuedContext,
            Exception exception,
            CancellationToken token)
        {
            var feedPackage = new FeedPackageIdentity(queuedContext.Package.Id, queuedContext.Package.Version);

            await _notificationService.OnPackageValidationFailedAsync(feedPackage.Id, feedPackage.Version, exception, token);

            var status = new PackageMonitoringStatus(feedPackage, exception);
            await _statusService.UpdateAsync(status, token);
        }

        /// <summary>
        /// Returns if the newest entry in <paramref name="first"/> is older than the newest entry in <paramref name="second"/>.
        /// </summary>
        private bool CompareCatalogEntries(IEnumerable<CatalogIndexEntry> first, IEnumerable<CatalogIndexEntry> second)
        {
            return first.Max(c => c.CommitTimeStamp) < second.Max(c => c.CommitTimeStamp);
        }
    }
}