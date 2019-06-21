// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue.Protocol;
using Ng.Helpers;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Sql;
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
        private CollectorHttpClient _client;

        public MonitoringProcessorJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
        {
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var gallery = arguments.GetOrThrow<string>(Arguments.Gallery);
            var index = arguments.GetOrThrow<string>(Arguments.Index);
            var packageBaseAddress = arguments.GetOrThrow<string>(Arguments.ContentBaseAddress);
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var requireRepositorySignature = arguments.GetOrDefault(Arguments.RequireRepositorySignature, false);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            var timeoutInSeconds = arguments.GetOrDefault(Arguments.SqlCommandTimeoutInSeconds, 300);
            var sqlTimeout = TimeSpan.FromSeconds(timeoutInSeconds);

            var connectionString = arguments.GetOrThrow<string>(Arguments.ConnectionString);
            var galleryDbConnection = new AzureSqlConnectionFactory(
                connectionString,
                new EmptySecretInjector(),
                LoggerFactory.CreateLogger<AzureSqlConnectionFactory>());
            var packageContentUriBuilder = new PackageContentUriBuilder(
                arguments.GetOrThrow<string>(Arguments.PackageContentUrlFormat));
            var galleryDatabase = new GalleryDatabaseQueryService(
                galleryDbConnection,
                packageContentUriBuilder,
                TelemetryService,
                timeoutInSeconds);

            CommandHelpers.AssertAzureStorage(arguments);

            var monitoringStorageFactory = CommandHelpers.CreateStorageFactory(arguments, verbose);
            var auditingStorageFactory = CommandHelpers.CreateSuffixedStorageFactory("Auditing", arguments, verbose);

            var endpointConfiguration = CommandHelpers.GetEndpointConfiguration(arguments);
            var messageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(TelemetryService, verbose);

            Logger.LogInformation(
                "CONFIG gallery: {Gallery} index: {Index} storage: {Storage} auditingStorage: {AuditingStorage} registration cursor uri: {RegistrationCursorUri} flat-container cursor uri: {FlatContainerCursorUri}",
                gallery, index, monitoringStorageFactory, auditingStorageFactory, endpointConfiguration.RegistrationCursorUri, endpointConfiguration.FlatContainerCursorUri);

            var validatorConfig = new ValidatorConfiguration(
                packageBaseAddress,
                requireRepositorySignature);

            _packageValidator = ValidationFactory.CreatePackageValidator(
                gallery,
                index,
                auditingStorageFactory,
                validatorConfig,
                endpointConfiguration,
                messageHandlerFactory,
                galleryDatabase,
                LoggerFactory);

            _queue = CommandHelpers.CreateStorageQueue<PackageValidatorContext>(arguments, PackageValidatorContext.Version);

            _statusService = CommandHelpers.GetPackageMonitoringStatusService(arguments, monitoringStorageFactory, LoggerFactory);

            _notificationService = new LoggerMonitoringNotificationService(LoggerFactory.CreateLogger<LoggerMonitoringNotificationService>());

            _client = new CollectorHttpClient(messageHandlerFactory());

            SetUserAgentString();
        }

        /// <remarks>
        /// Unfortunately, we have to use reflection to set the user agent string as we'd like to.
        /// See https://github.com/NuGet/Home/issues/7464
        /// </remarks>
        private static void SetUserAgentString()
        {
            var userAgentString = UserAgentUtility.GetUserAgent();

            typeof(UserAgent)
                .GetProperty(nameof(UserAgent.UserAgentString))
                .SetValue(null, userAgentString);
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            await ParallelAsync.Repeat(() => ProcessPackagesAsync(cancellationToken));
        }

        private async Task ProcessPackagesAsync(CancellationToken token)
        {
            StorageQueueMessage<PackageValidatorContext> queueMessage;
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
            catch (Exception validationFailedToRunException)
            {
                try
                {
                    // Validations failed to run! Save this failed status to storage.
                    await SaveFailedPackageMonitoringStatusAsync(queuedContext, validationFailedToRunException, token);
                    // We can then remove the message from the queue because this failed status can be used to requeue the message.
                    messageWasProcessed = true;
                }
                catch (Exception failedValidationSaveFailureException)
                {
                    // We failed to run validations and failed to save the failed validation!
                    // We were not able to process this message. We need to log the exceptions so we can debug the issue.
                    var aggregateException = new AggregateException(
                        "Validations failed to run and saving unsuccessful validation failed!",
                        new[] { validationFailedToRunException, failedValidationSaveFailureException });

                    Logger.LogCritical(
                        NuGet.Services.Metadata.Catalog.Monitoring.LogEvents.QueueMessageFatalFailure,
                        aggregateException,
                        "Failed to process queue message.");
                }
            }

            // If we failed to run validations and failed to save the failed validation, we cannot remove the message from the queue.
            if (messageWasProcessed)
            {
                try
                {
                    await _queue.RemoveAsync(queueMessage, token);
                }
                catch (StorageException storageException)
                {
                    if (storageException.RequestInformation.ExtendedErrorInformation.ErrorCode == QueueErrorCodeStrings.MessageNotFound)
                    {
                        Logger.LogWarning(
                            NuGet.Services.Metadata.Catalog.Monitoring.LogEvents.QueueMessageRemovalFailure,
                            storageException,
                            "Queue message for {PackageId} {PackageVersion} no longer exists. Message was likely handled by another instance of the job.",
                            queuedContext.Package.Id, queuedContext.Package.Version);
                    }
                    else
                    {
                        Logger.LogCritical(
                            NuGet.Services.Metadata.Catalog.Monitoring.LogEvents.QueueMessageRemovalFailure,
                            storageException,
                            "Failed to remove queue message.");
                    }
                }
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

            var existingStatus = await _statusService.GetAsync(feedPackage, token);

            if (catalogEntries != null && existingStatus?.ValidationResult?.CatalogEntries != null && CompareCatalogEntries(catalogEntries, existingStatus.ValidationResult.CatalogEntries))
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
            PackageMonitoringStatusAccessConditionHelper.UpdateFromExisting(status, existingStatus);

            await _statusService.UpdateAsync(status, token);
        }

        private async Task SaveFailedPackageMonitoringStatusAsync(
            PackageValidatorContext queuedContext,
            Exception exception,
            CancellationToken token)
        {
            var queuedVersion = queuedContext.Package.Version;
            var version = NuGetVersion.TryParse(queuedVersion, out var parsedVersion)
                ? parsedVersion.ToFullString() : queuedVersion;

            var feedPackage = new FeedPackageIdentity(queuedContext.Package.Id, version);

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