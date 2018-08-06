// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Versioning;
using NuGetGallery;

namespace NuGet.Services.Revalidate
{
    using IGalleryContext = IEntitiesContext;

    public class PackageFinder : IPackageFinder
    {
        public const string MicrosoftSetName = "Microsoft";
        public const string PreinstalledSetName = "Preinstalled";
        public const string DependencySetName = "Dependency";
        public const string RemainingSetName = "Remaining";

        private const string PreinstalledPackagesResource = "NuGet.Services.Revalidate.Initialization.PreinstalledPackages.json";

        private static int BatchSize = 1000;
        private static string MicrosoftAccountName = "Microsoft";

        private readonly IGalleryContext _galleryContext;
        private readonly InitializationConfiguration _config;
        private readonly ILogger<PackageFinder> _logger;

        public PackageFinder(
            IGalleryContext galleryContext,
            InitializationConfiguration config,
            ILogger<PackageFinder> logger)
        {
            _galleryContext = galleryContext ?? throw new ArgumentNullException(nameof(galleryContext));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public HashSet<int> FindMicrosoftPackages()
        {
            return FindRegistrationKeys(MicrosoftSetName, r => r.Owners.Any(o => o.Username == MicrosoftAccountName));
        }

        public HashSet<int> FindPreinstalledPackages(HashSet<int> except)
        {
            List<string> preinstalledPackagesNames;
            var assembly = typeof(PackageFinder).Assembly;

            using (var resource = assembly.GetManifestResourceStream(PreinstalledPackagesResource))
            using (var reader = new StreamReader(resource))
            {
                preinstalledPackagesNames = JsonConvert.DeserializeObject<List<string>>(reader.ReadToEnd());
            }

            var preinstalledPackages = FindRegistrationKeys(PreinstalledSetName, r => preinstalledPackagesNames.Contains(r.Id));

            preinstalledPackages.ExceptWith(except);

            return preinstalledPackages;
        }

        public HashSet<int> FindDependencyPackages(HashSet<int> roots)
        {
            var result = new HashSet<int>();
            var next = new HashSet<int>(roots);
            var seen = new HashSet<int>(roots);

            do
            {
                var dependencies = _galleryContext.PackageRegistrations
                    .Join(
                        _galleryContext.Set<PackageDependency>(),
                        registration => registration.Id,
                        dependency => dependency.Id,
                        (registration, dependency) => new
                        {
                            DependencyRegistrationKey = registration.Key,
                            ParentRegistrationKey = dependency.Package.PackageRegistrationKey
                        })
                    .Where(j => next.Contains(j.ParentRegistrationKey))
                    .Select(j => j.DependencyRegistrationKey)
                    .Distinct()
                    .ToList();

                next.Clear();
                next.UnionWith(dependencies);
                next.ExceptWith(seen);

                result.UnionWith(next);
                seen.UnionWith(next);
            }
            while (next.Count() > 0);

            return result;
        }

        public HashSet<int> FindAllPackages(HashSet<int> except)
        {
            return FindRegistrationKeys(RemainingSetName, r => !except.Contains(r.Key));
        }

        public List<PackageRegistrationInformation> FindPackageRegistrationInformation(string setName, HashSet<int> packageRegistrationKeys)
        {
            // Fetch the packages' information in batches.
            var batches = packageRegistrationKeys.Batch(BatchSize);
            var result = new List<PackageRegistrationInformation>();

            for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                _logger.LogInformation(
                    "Fetching batch {Batch} of {BatchesCount} of package informations for package set {SetName}...",
                    batchIndex + 1,
                    batches.Count,
                    setName);

                var batch = batches[batchIndex];

                var packages = _galleryContext.PackageRegistrations
                    .Where(r => batch.Contains(r.Key))
                    .Select(r => new PackageRegistrationInformation
                    {
                        Key = r.Key,
                        Id = r.Id,
                        Downloads = r.DownloadCount,
                        Versions = r.Packages.Count(),
                    });

                result.AddRange(packages);

                _logger.LogInformation(
                    "Fetched batch {Batch} of {BatchesCount} of package informations for package set {SetName}",
                    batchIndex + 1,
                    batches.Count,
                    setName);
            }

            return result;
        }

        public Dictionary<int, List<NuGetVersion>> FindAppropriateVersions(List<PackageRegistrationInformation> packages)
        {
            var maxCreated = _config.MaxPackageCreationDate.UtcDateTime;
            var keys = packages.Select(p => p.Key);

            var versions = _galleryContext.Set<Package>()
                .Where(p => keys.Contains(p.PackageRegistrationKey))
                .Where(p => p.PackageStatusKey == PackageStatus.Available || p.PackageStatusKey == PackageStatus.Deleted)
                .Where(p => p.Created < maxCreated)
                .Select(p => new { p.PackageRegistrationKey, p.NormalizedVersion })
                .ToList();

            return versions.GroupBy(p => p.PackageRegistrationKey)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => NuGetVersion.Parse(p.NormalizedVersion)).ToList());
        }

        public int AppropriatePackageCount()
        {
            var maxCreated = _config.MaxPackageCreationDate.UtcDateTime;

            return _galleryContext.Set<Package>()
                    .Where(p => p.PackageStatusKey == PackageStatus.Available || p.PackageStatusKey == PackageStatus.Deleted)
                    .Where(p => p.Created < maxCreated)
                    .Count();
        }

        private HashSet<int> FindRegistrationKeys(string setName, Expression<Func<PackageRegistration, bool>> match)
        {
            var result = new HashSet<int>();
            var start = -1;
            var done = false;

            while (!done)
            {
                var batchResults = _galleryContext
                    .PackageRegistrations
                    .Where(r => r.Key > start)
                    .Where(match)
                    .OrderBy(r => r.Key)
                    .Select(r => r.Key)
                    .Take(BatchSize)
                    .ToList();

                result.UnionWith(batchResults);

                _logger.LogInformation("Found {Results} results for package set {SetName}", result.Count, setName);

                if (batchResults.Count < BatchSize)
                {
                    done = true;
                }
                else
                {
                    start = batchResults.Last();

                    _logger.LogInformation(
                        "Sleeping for {SleepDuration} before searching for more package set {SetName} results",
                        _config.SleepDurationBetweenBatches,
                        setName);

                    Thread.Sleep(_config.SleepDurationBetweenBatches);
                }
            }

            return result;
        }
    }
}
