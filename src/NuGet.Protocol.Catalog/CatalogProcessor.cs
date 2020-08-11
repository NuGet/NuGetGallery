// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Catalog
{
    public class CatalogProcessor
    {
        private const string CatalogResourceType = "Catalog/3.0.0";
        private readonly ICatalogLeafProcessor _leafProcessor;
        private readonly ICatalogClient _client;
        private readonly ICursor _cursor;
        private readonly ILogger<CatalogProcessor> _logger;
        private readonly CatalogProcessorSettings _settings;

        public CatalogProcessor(
            ICursor cursor,
            ICatalogClient client,
            ICatalogLeafProcessor leafProcessor,
            CatalogProcessorSettings settings,
            ILogger<CatalogProcessor> logger)
        {
            _leafProcessor = leafProcessor ?? throw new ArgumentNullException(nameof(leafProcessor));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (settings.ServiceIndexUrl == null)
            {
                throw new ArgumentException(
                    $"The {nameof(CatalogProcessorSettings.ServiceIndexUrl)} property of the " +
                    $"{nameof(CatalogProcessorSettings)} must not be null.",
                    nameof(settings));
            }

            // Clone the settings to avoid mutability issues.
            _settings = settings.Clone();
        }

        /// <summary>
        /// Discovers and downloads all of the catalog leafs after the current cursor value and before the maximum
        /// commit timestamp found in the settings. Each catalog leaf is passed to the catalog leaf processor in
        /// chronological order. After a commit is completed, its commit timestamp is written to the cursor, i.e. when
        /// transitioning from commit timestamp A to B, A is written to the cursor so that it never is processed again.
        /// </summary>
        /// <returns>True if all of the catalog leaves found were processed successfully.</returns>
        public async Task<bool> ProcessAsync()
        {
            var catalogIndexUrl = await GetCatalogIndexUrlAsync();

            var minCommitTimestamp = await GetMinCommitTimestamp();
            _logger.LogInformation(
                "Using time bounds {min:O} (exclusive) to {max:O} (inclusive).",
                minCommitTimestamp,
                _settings.MaxCommitTimestamp);

            return await ProcessIndexAsync(catalogIndexUrl, minCommitTimestamp);
        }

        private async Task<bool> ProcessIndexAsync(string catalogIndexUrl, DateTimeOffset minCommitTimestamp)
        {
            var index = await _client.GetIndexAsync(catalogIndexUrl);

            var pageItems = index.GetPagesInBounds(
                minCommitTimestamp,
                _settings.MaxCommitTimestamp);
            _logger.LogInformation(
                "{pages} pages were in the time bounds, out of {totalPages}.",
                pageItems.Count,
                index.Items.Count);

            var success = true;
            for (var i = 0; i < pageItems.Count; i++)
            {
                success = await ProcessPageAsync(minCommitTimestamp, pageItems[i]);
                if (!success)
                {
                    _logger.LogWarning(
                        "{unprocessedPages} out of {pages} pages were left incomplete due to a processing failure.",
                        pageItems.Count - i,
                        pageItems.Count);
                    break;
                }
            }

            return success;
        }

        private async Task<bool> ProcessPageAsync(DateTimeOffset minCommitTimestamp, CatalogPageItem pageItem)
        {
            var page = await _client.GetPageAsync(pageItem.Url);

            var leafItems = page.GetLeavesInBounds(
                minCommitTimestamp,
                _settings.MaxCommitTimestamp,
                _settings.ExcludeRedundantLeaves);
            _logger.LogInformation(
                "On page {page}, {leaves} out of {totalLeaves} were in the time bounds.",
                pageItem.Url,
                leafItems.Count,
                page.Items.Count);

            DateTimeOffset? newCursor = null;
            var success = true;
            for (var i = 0; i < leafItems.Count; i++)
            {
                var leafItem = leafItems[i];

                if (newCursor.HasValue && newCursor.Value != leafItem.CommitTimestamp)
                {
                    await _cursor.SetAsync(newCursor.Value);
                }

                newCursor = leafItem.CommitTimestamp;

                success = await ProcessLeafAsync(leafItem);
                if (!success)
                {
                    _logger.LogWarning(
                        "{unprocessedLeaves} out of {leaves} leaves were left incomplete due to a processing failure.",
                        leafItems.Count - i,
                        leafItems.Count);
                    break;
                }
            }

            if (newCursor.HasValue && success)
            {
                await _cursor.SetAsync(newCursor.Value);
            }

            return success;
        }

        private async Task<bool> ProcessLeafAsync(CatalogLeafItem leafItem)
        {
            bool success;
            try
            {
                switch (leafItem.Type)
                {
                    case CatalogLeafType.PackageDelete:
                        var packageDelete = await _client.GetPackageDeleteLeafAsync(leafItem.Url);
                        success = await _leafProcessor.ProcessPackageDeleteAsync(packageDelete);
                        break;
                    case CatalogLeafType.PackageDetails:
                        var packageDetails = await _client.GetPackageDetailsLeafAsync(leafItem.Url);
                        success = await _leafProcessor.ProcessPackageDetailsAsync(packageDetails);
                        break;
                    default:
                        throw new NotSupportedException($"The catalog leaf type '{leafItem.Type}' is not supported.");
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    0,
                    exception,
                    "An exception was thrown while processing leaf {leafUrl}.",
                    leafItem.Url);
                success = false;
            }

            if (!success)
            {
                _logger.LogWarning(
                    "Failed to process leaf {leafUrl} ({packageId} {packageVersion}, {leafType}).",
                    leafItem.Url,
                    leafItem.PackageId,
                    leafItem.PackageVersion,
                    leafItem.Type);
            }

            return success;
        }

        private async Task<DateTimeOffset> GetMinCommitTimestamp()
        {
            var minCommitTimestamp = await _cursor.GetAsync();

            minCommitTimestamp = minCommitTimestamp
                ?? _settings.DefaultMinCommitTimestamp
                ?? _settings.MinCommitTimestamp;

            if (minCommitTimestamp.Value < _settings.MinCommitTimestamp)
            {
                minCommitTimestamp = _settings.MinCommitTimestamp;
            }

            return minCommitTimestamp.Value;
        }

        private async Task<string> GetCatalogIndexUrlAsync()
        {
            _logger.LogInformation("Getting catalog index URL from {serviceIndexUrl}.", _settings.ServiceIndexUrl);
            string catalogIndexUrl;
            var sourceRepository = Repository.Factory.GetCoreV3(_settings.ServiceIndexUrl, FeedType.HttpV3);
            var serviceIndexResource = await sourceRepository.GetResourceAsync<ServiceIndexResourceV3>();
            catalogIndexUrl = serviceIndexResource.GetServiceEntryUri(CatalogResourceType)?.AbsoluteUri;
            if (catalogIndexUrl == null)
            {
                throw new InvalidOperationException(
                    $"The service index does not contain resource '{CatalogResourceType}'.");
            }

            return catalogIndexUrl;
        }
    }
}
