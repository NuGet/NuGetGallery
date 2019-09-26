// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationCollector : SortingGraphCollector
    {
        public const int PartitionSize = 64;
        public const int PackageCountThreshold = 128;

        // This is simply the arbitrary limit that I tested.  There may be a better value.
        public const int DefaultMaxConcurrentBatches = 10;

        private readonly StorageFactory _legacyStorageFactory;
        private readonly StorageFactory _semVer2StorageFactory;
        private readonly ShouldIncludeRegistrationPackage _shouldIncludeSemVer2ForLegacyStorageFactory;
        private readonly RegistrationMakerCatalogItem.PostProcessGraph _postProcessGraphForLegacyStorageFactory;
        private readonly bool _forcePackagePathProviderForIcons;
        private readonly int _maxConcurrentBatches;
        private readonly ILogger _logger;

        public RegistrationCollector(
            Uri index,
            StorageFactory legacyStorageFactory,
            StorageFactory semVer2StorageFactory,
            Uri contentBaseAddress,
            Uri galleryBaseAddress,
            bool forcePackagePathProviderForIcons,
            ITelemetryService telemetryService,
            ILogger logger,
            Func<HttpMessageHandler> handlerFunc = null,
            IHttpRetryStrategy httpRetryStrategy = null,
            int maxConcurrentBatches = DefaultMaxConcurrentBatches)
            : base(
                  index,
                  new Uri[] { Schema.DataTypes.PackageDetails, Schema.DataTypes.PackageDelete },
                  telemetryService,
                  handlerFunc,
                  httpRetryStrategy)
        {
            _legacyStorageFactory = legacyStorageFactory ?? throw new ArgumentNullException(nameof(legacyStorageFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _semVer2StorageFactory = semVer2StorageFactory;
            _shouldIncludeSemVer2ForLegacyStorageFactory = GetShouldIncludeRegistrationPackageForLegacyStorageFactory(_semVer2StorageFactory);
            _postProcessGraphForLegacyStorageFactory = GetPostProcessGraphForLegacyStorageFactory(_semVer2StorageFactory);
            ContentBaseAddress = contentBaseAddress;
            GalleryBaseAddress = galleryBaseAddress;
            _forcePackagePathProviderForIcons = forcePackagePathProviderForIcons;

            if (maxConcurrentBatches < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxConcurrentBatches),
                    maxConcurrentBatches,
                    string.Format(Strings.ArgumentOutOfRange, 1, int.MaxValue));
            }

            _maxConcurrentBatches = maxConcurrentBatches;
        }

        public Uri ContentBaseAddress { get; }
        public Uri GalleryBaseAddress { get; }

        protected override Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(
            IEnumerable<CatalogCommitItem> catalogItems)
        {
            // Grouping batches by commit is slow if it contains
            // the same package registration id over and over again.
            // This happens when, for example, a package publish "wave"
            // occurs.
            //
            // If one package registration id is part of 20 batches,
            // we'll have to process all registration leafs 20 times.
            // It would be better to process these leafs only once.
            //
            // So let's batch by package registration id here,
            // ensuring we never write a commit timestamp to the cursor
            // that is higher than the last item currently processed.
            //
            // So, group by id, then make sure the batch key is the
            // *lowest*  timestamp of all commits in that batch.
            // This ensures that on retries, we will retry
            // from the correct location (even though we may have
            // a little rework).

            var batches = CatalogCommitUtilities.CreateCommitItemBatches(catalogItems, GetKey);

            return Task.FromResult(batches);
        }

        protected override string GetKey(CatalogCommitItem item)
        {
            return CatalogCommitUtilities.GetPackageIdKey(item);
        }

        // Summary:
        //
        //      1.  Process one catalog page at a time.
        //      2.  Within a given catalog page, batch catalog commit entries by lower-cased package ID.
        //      3.  Process up to `n` batches in parallel.  Note that the batches may span multiple catalog commits.
        //      4.  Cease processing new batches if a failure has been observed.  This job will eventually retry
        //          batches on its next outermost job loop.
        //      5.  If a failure has been observed, wait for all existing tasks to complete.  Avoid task cancellation
        //          as that could lead to the entirety of a package registration being in an inconsistent state.
        //          To be fair, a well-timed exception could have the same result, but registration updates have never
        //          been transactional.  Actively cancelling tasks would make an inconsistent registration more likely.
        //      6.  Update the cursor if and only if all preceding commits and the current (oldest) commit have been
        //          fully and successfully processed.
        protected override Task<bool> FetchAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            ReadCursor back,
            CancellationToken cancellationToken)
        {
            return CatalogCommitUtilities.ProcessCatalogCommitsAsync(
                client,
                front,
                back,
                FetchCatalogCommitsAsync,
                CreateBatchesAsync,
                ProcessBatchAsync,
                _maxConcurrentBatches,
                _logger,
                cancellationToken);
        }

        protected override async Task ProcessGraphsAsync(
            KeyValuePair<string, IReadOnlyDictionary<string, IGraph>> sortedGraphs,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            using (_telemetryService.TrackDuration(TelemetryConstants.ProcessGraphsSeconds,
                new Dictionary<string, string>()
                {
                    { TelemetryConstants.Id, sortedGraphs.Key.ToLowerInvariant() }
                }))
            {
                var legacyTask = RegistrationMaker.ProcessAsync(
                    registrationKey: new RegistrationKey(sortedGraphs.Key),
                    newItems: sortedGraphs.Value,
                    shouldInclude: _shouldIncludeSemVer2ForLegacyStorageFactory,
                    storageFactory: _legacyStorageFactory,
                    postProcessGraph: _postProcessGraphForLegacyStorageFactory,
                    contentBaseAddress: ContentBaseAddress,
                    galleryBaseAddress: GalleryBaseAddress,
                    partitionSize: PartitionSize,
                    packageCountThreshold: PackageCountThreshold,
                    forcePackagePathProviderForIcons: _forcePackagePathProviderForIcons,
                    telemetryService: _telemetryService,
                    cancellationToken: cancellationToken);
                tasks.Add(legacyTask);

                if (_semVer2StorageFactory != null)
                {
                    var semVer2Task = RegistrationMaker.ProcessAsync(
                        registrationKey: new RegistrationKey(sortedGraphs.Key),
                        newItems: sortedGraphs.Value,
                        storageFactory: _semVer2StorageFactory,
                        contentBaseAddress: ContentBaseAddress,
                        galleryBaseAddress: GalleryBaseAddress,
                        partitionSize: PartitionSize,
                        packageCountThreshold: PackageCountThreshold,
                        forcePackagePathProviderForIcons: _forcePackagePathProviderForIcons,
                        telemetryService: _telemetryService,
                        cancellationToken: cancellationToken);
                    tasks.Add(semVer2Task);
                }

                await Task.WhenAll(tasks);
            }
        }

        public static ShouldIncludeRegistrationPackage GetShouldIncludeRegistrationPackageForLegacyStorageFactory(StorageFactory semVer2StorageFactory)
        {
            // If SemVer 2.0.0 storage is disabled, put SemVer 2.0.0 registration in the legacy storage factory. In no
            // case should a package be completely ignored. That is, if a package is SemVer 2.0.0 but SemVer 2.0.0
            // storage is not enabled, our only choice is to put SemVer 2.0.0 packages in the legacy storage.
            if (semVer2StorageFactory == null)
            {
                return (k, u, g) => true;
            }

            return (k, u, g) => !NuGetVersionUtility.IsGraphSemVer2(k.Version, u, g);
        }

        public static RegistrationMakerCatalogItem.PostProcessGraph GetPostProcessGraphForLegacyStorageFactory(StorageFactory semVer2StorageFactory)
        {
            // If SemVer 2.0.0 storage is disabled, put deprecation metadata in the legacy storage.
            // A package's deprecation metadata should never be completely ignored.
            // If a package contains deprecation metadata but SemVer 2.0.0 storage is not enabled,
            // our only choice is to put deprecation metadata in the legacy storage.
            if (semVer2StorageFactory == null)
            {
                return g => g;
            }

            return FilterOutDeprecationInformation;
        }

        public static RegistrationMakerCatalogItem.PostProcessGraph FilterOutDeprecationInformation = g =>
        {
            var deprecationTriples = g
                .GetTriplesWithPredicate(g.CreateUriNode(Schema.Predicates.Deprecation))
                .ToList();

            g.Retract(deprecationTriples);
            return g;
        };

        private async Task ProcessBatchAsync(
            CollectorHttpClient client,
            JToken context,
            string packageId,
            CatalogCommitItemBatch batch,
            CatalogCommitItemBatch lastBatch,
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            using (_telemetryService.TrackDuration(
                TelemetryConstants.ProcessBatchSeconds,
                new Dictionary<string, string>()
                {
                    { TelemetryConstants.Id, packageId },
                    { TelemetryConstants.BatchItemCount, batch.Items.Count.ToString() }
                }))
            {
                await OnProcessBatchAsync(
                    client,
                    batch.Items,
                    context,
                    batch.CommitTimeStamp,
                    isLastBatch: false,
                    cancellationToken: cancellationToken);
            }
        }
    }
}