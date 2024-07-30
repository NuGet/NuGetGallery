// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Services.PackageHash
{
    public class BatchProcessor : IBatchProcessor
    {
        private readonly IPackageHashCalculator _calculator;
        private readonly IOptionsSnapshot<PackageHashConfiguration> _configuration;
        private readonly ILogger<BatchProcessor> _logger;

        public BatchProcessor(
            IPackageHashCalculator calculator,
            IOptionsSnapshot<PackageHashConfiguration> configuration,
            ILogger<BatchProcessor> logger)
        {
            _calculator = calculator ?? throw new ArgumentNullException(nameof(configuration));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<InvalidPackageHash>> ProcessBatchAsync(
            IReadOnlyList<PackageHash> batch,
            string hashAlgorithmId,
            CancellationToken token)
        {
            // Build up a bag of work.
            var remainingWork = new ConcurrentBag<Work>();
            var failures = new ConcurrentBag<InvalidPackageHash>();
            foreach (var source in _configuration.Value.Sources)
            {
                foreach (var package in batch)
                {
                    remainingWork.Add(new Work(source, package));
                }
            }

            // Perform the work in parallel.
            _logger.LogInformation(
                "Starting to check hashes for {PackageCount} packages from {SourceCount} sources ({WorkCount} total" +
                " checks, {DegreeOfParallelism} tasks).",
                batch.Count,
                _configuration.Value.Sources.Count,
                remainingWork.Count,
                _configuration.Value.DegreeOfParallelism);

            var tasks = Enumerable
                .Range(0, _configuration.Value.DegreeOfParallelism)
                .Select(x => ProcessWorkAsync(remainingWork, failures, hashAlgorithmId, token))
                .ToList();

            await Task.WhenAll(tasks);

            _logger.LogInformation(
                "Completed the batch. {FailureResultCount} failure results were found.",
                failures.Count);

            return failures.ToList();
        }

        private async Task ProcessWorkAsync(
            ConcurrentBag<Work> remainingWork,
            ConcurrentBag<InvalidPackageHash> failures,
            string hashAlgorithmId,
            CancellationToken token)
        {
            while (remainingWork.TryTake(out var work) && !token.IsCancellationRequested)
            {
                var actualHash = await _calculator.GetPackageHashAsync(
                    work.Source,
                    work.Package.Identity,
                    hashAlgorithmId,
                    token);

                if (work.Package.ExpectedHash != actualHash)
                {
                    failures.Add(new InvalidPackageHash(work.Source, work.Package, actualHash));
                }
            }
        }

        private class Work
        {
            public Work(PackageSource source, PackageHash package)
            {
                Source = source ?? throw new ArgumentNullException(nameof(source));
                Package = package ?? throw new ArgumentNullException(nameof(package));
            }

            public PackageSource Source { get; }
            public PackageHash Package { get; }
        }
    }
}
