// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;
using NuGet.Versioning;

namespace NuGet.Services.Revalidate
{
    public class InitializationManager
    {
        private static int BatchSize = 1000;

        private readonly IRevalidationJobStateService _jobState;
        private readonly IPackageRevalidationStateService _packageState;
        private readonly IPackageFinder _packageFinder;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly InitializationConfiguration _config;
        private readonly ILogger<InitializationManager> _logger;

        public InitializationManager(
            IRevalidationJobStateService jobState,
            IPackageRevalidationStateService packageState,
            IPackageFinder packageFinder,
            IServiceScopeFactory scopeFactory,
            InitializationConfiguration config,
            ILogger<InitializationManager> logger)
        {
            _jobState = jobState ?? throw new ArgumentNullException(nameof(jobState));
            _packageState = packageState ?? throw new ArgumentNullException(nameof(packageState));
            _packageFinder = packageFinder ?? throw new ArgumentNullException(nameof(packageFinder));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync()
        {
            if (await _jobState.IsInitializedAsync())
            {
                _logger.LogError("Attempted to initialize the revalidation job when it is already initialized!");

                throw new InvalidOperationException("Attempted to initialize the revalidation job when it is already initialized!");
            }

            await ClearPackageRevalidationStateAsync();

            // Find packages owned by Microsoft or preinstalled by Visual Studio.
            var microsoftPackages = _packageFinder.FindMicrosoftPackages();
            var preinstalledPackages = _packageFinder.FindPreinstalledPackages(except: microsoftPackages);

            // Find the packages depended by both Microsoft and preinstalled packages.
            var microsoftOrPreinstalledPackages = new HashSet<int>();

            microsoftOrPreinstalledPackages.UnionWith(microsoftPackages);
            microsoftOrPreinstalledPackages.UnionWith(preinstalledPackages);

            var dependencyPackages = _packageFinder.FindDependencyPackages(microsoftOrPreinstalledPackages);

            // Find the set of all other packages.
            var knownPackages = new HashSet<int>();

            knownPackages.UnionWith(microsoftOrPreinstalledPackages);
            knownPackages.UnionWith(dependencyPackages);

            var remainingPackages = _packageFinder.FindAllPackages(except: knownPackages);

            // Save the packages that were found, by order of priority.
            await InitializePackageSetAsync(PackageFinder.MicrosoftSetName, microsoftPackages);
            await InitializePackageSetAsync(PackageFinder.PreinstalledSetName, preinstalledPackages);
            await InitializePackageSetAsync(PackageFinder.DependencySetName, dependencyPackages);
            await InitializePackageSetAsync(PackageFinder.RemainingSetName, remainingPackages);

            await _jobState.MarkAsInitializedAsync();
        }

        public async Task VerifyInitializationAsync()
        {
            if (!await _jobState.IsInitializedAsync())
            {
                _logger.LogError("Expected revalidation state to be initialized");

                throw new Exception("Expected revalidation state to be initialized");
            }

            var expectedCount = _packageFinder.AppropriatePackageCount();
            var actualCount = await _packageState.PackageRevalidationCountAsync();

            if (actualCount != expectedCount)
            {
                _logger.LogError("Expected {ExpectedRevalidations} revalidations, found {ActualRevalidations}", expectedCount, actualCount);

                throw new Exception($"Expected {expectedCount} revalidation, found {actualCount}");
            }
        }

        private async Task ClearPackageRevalidationStateAsync()
        {
            _logger.LogInformation("Clearing package revalidation state, if it exists...");

            int removedRevalidations;

            do
            {
                removedRevalidations = await _packageState.RemovePackageRevalidationsAsync(BatchSize);

                if (removedRevalidations > 0)
                {
                    _logger.LogInformation(
                        "Deleted {RemovedRevalidations} revalidations. Sleeping for {SleepDuration}...",
                        removedRevalidations,
                        _config.SleepDurationBetweenBatches);

                    await Task.Delay(_config.SleepDurationBetweenBatches);
                }
            }
            while (removedRevalidations == BatchSize);

            _logger.LogInformation("Cleared package revalidation state");
        }

        private async Task InitializePackageSetAsync(string setName, HashSet<int> packageRegistrationKeys)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var scopedPackageFinder = scope.ServiceProvider.GetRequiredService<IPackageFinder>();
                var scopedJobState = scope.ServiceProvider.GetRequiredService<IRevalidationJobStateService>();
                var scopedScopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

                var packageInformations = await scopedPackageFinder.FindPackageRegistrationInformationAsync(setName, packageRegistrationKeys);
                var chunks = packageInformations
                    .OrderByDescending(p => p.Downloads)
                    .WeightedBatch(BatchSize, p => p.Versions);

                for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
                {
                    while (await scopedJobState.IsKillswitchActiveAsync())
                    {
                        _logger.LogInformation(
                            "Delaying initialization of chunk {Chunk} of {Chunks} for package set {SetName} due to active killswitch",
                            chunkIndex + 1,
                            chunks.Count,
                            setName);

                        await Task.Delay(_config.SleepDurationBetweenBatches);
                    }

                    await InitializePackageSetChunkAsync(setName, chunks, chunkIndex, scopedScopeFactory, _logger);

                    // Sleep if this is not the last chunk to prevent overloading the database.
                    if (chunkIndex < chunks.Count - 1)
                    {
                        _logger.LogInformation(
                            "Sleeping for {SleepDuration} before initializing the next chunk...",
                            _config.SleepDurationBetweenBatches);

                        await Task.Delay(_config.SleepDurationBetweenBatches);
                    }
                }

                _logger.LogInformation("Finished initializing package set {SetName}", setName);
            }
        }

        private static async Task InitializePackageSetChunkAsync(
            string setName,
            List<List<PackageRegistrationInformation>> chunks,
            int chunkIndex,
            IServiceScopeFactory scopeFactory,
            ILogger<InitializationManager> logger)
        {
            logger.LogInformation(
                "Initializing chunk {Chunk} of {Chunks} for package set {SetName}...",
                chunkIndex + 1,
                chunks.Count,
                setName);

            using (var scope = scopeFactory.CreateScope())
            {
                var scopedPackageState = scope.ServiceProvider.GetRequiredService<IPackageRevalidationStateService>();
                var scopedPackageFinder = scope.ServiceProvider.GetRequiredService<IPackageFinder>();

                var chunk = chunks[chunkIndex];
                var versions = scopedPackageFinder.FindAppropriateVersions(chunk);

                await InitializeRevalidationsAsync(chunk, versions, scopedPackageState, logger);

                logger.LogInformation(
                    "Initialized chunk {Chunk} of {Chunks} for package set {SetName}",
                    chunkIndex + 1,
                    chunks.Count,
                    setName);
            }
        }

        private static async Task InitializeRevalidationsAsync(
            List<PackageRegistrationInformation> packageRegistrations,
            Dictionary<int, List<NuGetVersion>> versions,
            IPackageRevalidationStateService packageState,
            ILogger<InitializationManager> logger)
        {
            var revalidations = new List<PackageRevalidation>();

            foreach (var packageRegistration in packageRegistrations)
            {
                var packageId = packageRegistration.Id;

                if (!versions.ContainsKey(packageRegistration.Key) || versions[packageRegistration.Key].Count == 0)
                {
                    logger.LogWarning("Could not find any versions of package {PackageId} to revalidate", packageId);

                    continue;
                }

                // Insert each version of the package in descending order of the versions.
                var packageVersions = versions[packageRegistration.Key].OrderByDescending(v => v);

                foreach (var version in packageVersions)
                {
                    revalidations.Add(new PackageRevalidation
                    {
                        PackageId = packageId,
                        PackageNormalizedVersion = version.ToNormalizedString(),
                        ValidationTrackingId = Guid.NewGuid(),
                        Completed = false,
                        Enqueued = null,
                    });
                }
            }

            await packageState.AddPackageRevalidationsAsync(revalidations);
        }
    }
}
