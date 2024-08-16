// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging.Core;
using NuGet.Services.Cursor;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;

namespace NuGet.Services.PackageHash
{
    public class PackageHashProcessor : IPackageHashProcessor
    {
        /// <summary>
        /// If the cursor is close to the current time, look back a certain amount of time to catch any packages that
        /// were updated out of order.
        /// </summary>
        private static readonly TimeSpan LookbackWindow = TimeSpan.FromHours(1);

        private readonly IEntityRepository<Package> _packageRepository;
        private readonly IBatchProcessor _batchProcessor;
        private readonly IResultRecorder _resultRecorder;
        private readonly IOptionsSnapshot<PackageHashConfiguration> _configuration;
        private readonly DurableCursor _cursor;
        private readonly ILogger<PackageHashProcessor> _logger;

        public PackageHashProcessor(
            IEntityRepository<Package> packageRepository,
            IBatchProcessor batchProcessor,
            IResultRecorder resultRecorder,
            IOptionsSnapshot<PackageHashConfiguration> configuration,
            DurableCursor cursor,
            ILogger<PackageHashProcessor> logger)
        {
            _packageRepository = packageRepository ?? throw new ArgumentNullException(nameof(packageRepository));
            _batchProcessor = batchProcessor ?? throw new ArgumentNullException(nameof(batchProcessor));
            _resultRecorder = resultRecorder ?? throw new ArgumentNullException(nameof(resultRecorder));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExecuteAsync(
            int bucketNumber,
            int bucketCount,
            CancellationToken token)
        {
            DateTimeOffset? newCursorValue;
            do
            {
                await _cursor.Load(token);

                newCursorValue = await ProcessBatchAsync(bucketNumber, bucketCount, token);

                if (newCursorValue.HasValue)
                {
                    _cursor.Value = newCursorValue.Value;
                    _logger.LogInformation(
                        "Moving cursor {BucketNumber}/{BucketCount} forward to {NewCursorValue}.",
                        bucketNumber,
                        bucketCount,
                        newCursorValue.Value);
                    await _cursor.Save(token);
                }
            }
            while (newCursorValue.HasValue);
        }

        private async Task<DateTimeOffset?> ProcessBatchAsync(
            int bucketNumber,
            int bucketCount,
            CancellationToken token)
        {
            var cursorValue = _cursor.Value.UtcDateTime;
            var lookbackTime = DateTime.UtcNow.Subtract(LookbackWindow);
            var lookbackApplied = false;
            if (cursorValue > lookbackTime)
            {
                _logger.LogInformation(
                    "Since the cursor is close to the current time, the cursor value {CursorValue} will be used instead of {OriginalCursorValue}.",
                    lookbackTime,
                    cursorValue);
                cursorValue = lookbackTime;
                lookbackApplied = true;
            }

            _logger.LogInformation(
                "Querying for up to {MaxBatchSize} packages with max timestamp after {CursorValue}.",
                _configuration.Value.BatchSize,
                cursorValue);

            var fullBatch = await _packageRepository
                .GetAll()
                .Include(x => x.PackageRegistration)
                .Where(p => p.PackageStatusKey == PackageStatus.Available
                            && (p.Created > cursorValue
                                || (p.LastEdited.HasValue && p.LastEdited.Value > cursorValue)))
                .OrderBy(p => p.LastEdited.HasValue && p.LastEdited.Value > p.Created ? p.LastEdited.Value : p.Created)
                .Take(_configuration.Value.BatchSize)
                .ToListAsync();

            if (!fullBatch.Any())
            {
                _logger.LogInformation("No more packages to process.");
                return null;
            }

            _logger.LogInformation("Found {BatchSize} packages packages to process.", fullBatch.Count);

            // Exclude packages sharing the same max timestamp. We do this since our cursor is exclusive and it's
            // possible the current batch does not have all of the packages with the same max timestamp.
            var maxTimestamp = fullBatch.Select(GetMaxTimestamp).Max();
            var trimmedBatch = fullBatch
                .Where(x => GetMaxTimestamp(x) < maxTimestamp)
                .ToList();

            if (!trimmedBatch.Any())
            {
                if (fullBatch.Count == _configuration.Value.BatchSize)
                {
                    _logger.LogError(
                        "All of the packages in the batch have the same max timestamp of {MaxTimestamp}. " +
                        "Consider increasing the batch size before continuing.",
                        maxTimestamp);
                }
                else
                {
                    _logger.LogInformation(
                        "The reamining packages will be left unprocessed until a timestamp newer than " +
                        "{MaxTimestamp} is encountered.",
                        maxTimestamp);
                }
                                
                return null;
            }

            var unexpectedHashAlgorithms = trimmedBatch
                .Select(x => x.HashAlgorithm)
                .Where(x => x != CoreConstants.Sha512HashAlgorithmId)
                .Distinct()
                .ToList();
            if (unexpectedHashAlgorithms.Any())
            {
                _logger.LogError(
                    "At least one package with an unexpected hash algorithms was found: {UnexpectedHashAlgorithms}",
                    unexpectedHashAlgorithms);
                return null;
            }

            var failureResults = await PartitionAndProcessAsync(bucketNumber, bucketCount, trimmedBatch, token);

            if (failureResults.Any())
            {
                await _resultRecorder.RecordAsync(failureResults);
            }

            if (lookbackApplied)
            {
                return null;
            }

            return new DateTimeOffset(trimmedBatch.Select(GetMaxTimestamp).Max(), TimeSpan.Zero);
        }

        private async Task<IReadOnlyList<InvalidPackageHash>> PartitionAndProcessAsync(
            int bucketNumber,
            int bucketCount,
            IReadOnlyList<Package> batch,
            CancellationToken token)
        {
            var packages = batch
                .Select(x => new PackageHash(
                    new PackageIdentity(
                        x.PackageRegistration.Id,
                        NuGetVersion.Parse(x.NormalizedVersion)),
                    x.Hash))
                .Where(x => MatchesBucket(bucketNumber, bucketCount, x))
                .ToList();

            _logger.LogInformation(
                "{PackageCount} packages are in bucket {BucketNumber}/{BucketCount} from a batch of size {BatchSize}.",
                packages.Count,
                bucketNumber,
                bucketCount,
                batch.Count);

            return await _batchProcessor.ProcessBatchAsync(
                packages,
                CoreConstants.Sha512HashAlgorithmId,
                token);
        }

        private bool MatchesBucket(int bucketNumber, int bucketCount, PackageHash x)
        {
            if (bucketCount == 1)
            {
                return true;
            }

            var key = $"{x.Identity.Id}/{x.Identity.Version.ToNormalizedString()}".ToLowerInvariant();

            var bucketIndex = ConsistentHash.DetermineBucket(key, bucketCount);

            // Bucket index is zero-based. Bucket number is one-based.
            return bucketIndex == bucketNumber - 1;
        }

        private DateTime GetMaxTimestamp(Package package)
        {
            return new[]
            {
                package.Created,
                package.LastEdited ?? DateTime.MinValue,
            }.Max();
        }
    }
}
