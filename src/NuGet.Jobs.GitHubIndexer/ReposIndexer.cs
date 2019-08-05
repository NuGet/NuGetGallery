// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGetGallery;

namespace NuGet.Jobs.GitHubIndexer
{
    public class ReposIndexer
    {
        private const string WorkingDirectory = "work";
        private const string BlobStorageContainerName = "content";
        private const string GitHubUsageFileName = "GitHubUsage.v1.json";
        public const int MaxBlobSizeBytes = 1 << 20; // 1 MB = 2^20

        public static readonly string RepositoriesDirectory = Path.Combine(WorkingDirectory, "repos");
        public static readonly string CacheDirectory = Path.Combine(WorkingDirectory, "cache");
        private static readonly JsonSerializer Serializer = new JsonSerializer();

        private readonly IGitRepoSearcher _searcher;
        private readonly ILogger<ReposIndexer> _logger;
        private readonly int _maxDegreeOfParallelism;
        private readonly IRepositoriesCache _repoCache;
        private readonly IRepoFetcher _repoFetcher;
        private readonly IConfigFileParser _configFileParser;
        private readonly ICloudBlobClient _cloudClient;
        private readonly ITelemetryService _telemetry;

        public ReposIndexer(
            IGitRepoSearcher searcher,
            ILogger<ReposIndexer> logger,
            IRepositoriesCache repoCache,
            IConfigFileParser configFileParser,
            IRepoFetcher repoFetcher,
            ICloudBlobClient cloudClient,
            IOptionsSnapshot<GitHubIndexerConfiguration> configuration,
            ITelemetryService telemetry)
        {
            _searcher = searcher ?? throw new ArgumentNullException(nameof(searcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repoCache = repoCache ?? throw new ArgumentNullException(nameof(repoCache));
            _configFileParser = configFileParser ?? throw new ArgumentNullException(nameof(configFileParser));
            _repoFetcher = repoFetcher ?? throw new ArgumentNullException(nameof(repoFetcher));

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _maxDegreeOfParallelism = configuration.Value.MaxDegreeOfParallelism;
            _cloudClient = cloudClient ?? throw new ArgumentNullException(nameof(cloudClient));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        public async Task RunAsync()
        {
            var repos = await _searcher.GetPopularRepositories();
            var inputBag = new ConcurrentBag<WritableRepositoryInformation>(repos);
            var outputBag = new ConcurrentBag<RepositoryInformation>();

            // Create the repos and cache directories
            Directory.CreateDirectory(RepositoriesDirectory);
            Directory.CreateDirectory(CacheDirectory);

            await ProcessInParallel(inputBag, repo =>
            {
                try
                {
                    outputBag.Add(ProcessSingleRepo(repo));
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, "[{RepoName}] Can't process repo", repo.Id);
                }
            });

            var finalList = outputBag
                .Where(repo => repo.Dependencies.Any())
                .OrderByDescending(x => x.Stars)
                .ThenBy(x => x.Id)
                .ToList();

            if (finalList.Any())
            {
                using (_telemetry.TrackUploadGitHubUsageBlobDuration())
                {
                    _logger.LogInformation("Uploading the GitHub Usage blob...");
                    await WriteFinalBlobAsync(finalList);
                    _logger.LogInformation("Uploaded the GitHub Usage blob...");
                }
            }
            else
            {
                _telemetry.TrackEmptyGitHubUsageBlob();
                _logger.LogError("The final GitHub Usage blob is empty!");
            }

            // Delete the repos and cache directory
            Directory.Delete(RepositoriesDirectory, recursive: true);
            Directory.Delete(CacheDirectory, recursive: true);
        }

        private async Task WriteFinalBlobAsync(List<RepositoryInformation> finalList)
        {
            var blobReference = _cloudClient.GetContainerReference(BlobStorageContainerName).GetBlobReference(GitHubUsageFileName);

            using (var stream = await blobReference.OpenWriteAsync(accessCondition: null))
            using (var streamWriter = new StreamWriter(stream))
            using (var jsonTextWriter = new JsonTextWriter(streamWriter))
            {
                blobReference.Properties.ContentType = "application/json";
                Serializer.Serialize(jsonTextWriter, finalList);
            }
        }

        private RepositoryInformation ProcessSingleRepo(WritableRepositoryInformation repo)
        {
            if (_repoCache.TryGetCachedVersion(repo, out var cachedVersion))
            {
                return cachedVersion;
            }

            using (_telemetry.TrackIndexRepositoryDuration(repo.Id))
            using (_logger.BeginScope("Starting indexing for repo {Name}", repo.Id))
            using (var fetchedRepo = _repoFetcher.FetchRepo(repo))
            {
                IReadOnlyList<GitFileInfo> files;
                using (_telemetry.TrackListFilesDuration(repo.Id))
                {
                    _logger.LogInformation("Finding files in repo {Name}...", repo.Id);

                    files = fetchedRepo.GetFileInfos();
                }

                var filePaths = files.Where(ShouldCheckOutFile).Select(x => x.Path).ToList();

                IReadOnlyList<ICheckedOutFile> checkedOutFiles;
                using (_telemetry.TrackCheckOutFilesDuration(repo.Id))
                {
                    _logger.LogInformation(
                        "Checking out {FileCount} files from repo {Name}",
                        filePaths.Count,
                        repo.Id);

                    checkedOutFiles = fetchedRepo.CheckoutFiles(filePaths);
                }

                foreach (var cfgFile in checkedOutFiles)
                {
                    var dependencies = _configFileParser.Parse(cfgFile);
                    repo.AddDependencies(dependencies);
                }

                _logger.LogInformation("Finished indexing repo {Name}", repo.Id);
            }

            var result = repo.ToRepositoryInformation();
            _repoCache.Persist(result);
            return result;
        }

        private bool ShouldCheckOutFile(GitFileInfo file)
        {
            // Only checkout files that are known to declare NuGet dependencies.
            if (Filters.GetConfigFileType(file.Path) == Filters.ConfigFileType.None)
            {
                return false;
            }

            if (file.BlobSize > MaxBlobSizeBytes)
            {
                _logger.LogWarning("File is too big! {FilePath} {FileSizeBytes} bytes", file.Path, file.BlobSize);
                return false;
            }

            return true;
        }

        private async Task ProcessInParallel<T>(ConcurrentBag<T> items, Action<T> work)
        {
            using (var sem = new SemaphoreSlim(_maxDegreeOfParallelism))
            {
                for (int i = 0; i < _maxDegreeOfParallelism; ++i)
                {
                    await sem.WaitAsync();
                    var thread = new Thread(() =>
                        {
                            while (items.TryTake(out var item))
                            {
                                work(item);
                            }
                            sem.Release();
                        });
                    thread.IsBackground = true; // This is important as it allows the process to exit while this thread is running
                    thread.Start();
                }

                // Wait for all Threads to complete
                for (int i = 0; i < _maxDegreeOfParallelism; ++i)
                {
                    await sem.WaitAsync();
                }
            }
        }
    }
}
