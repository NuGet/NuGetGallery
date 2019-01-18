// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch.Wrappers;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchStatusService : ISearchStatusService
    {
        private readonly ISearchIndexClientWrapper _searchIndex;
        private readonly ISearchIndexClientWrapper _hijackIndex;
        private readonly IAuxiliaryDataCache _auxiliaryDataCache;
        private readonly IOptionsSnapshot<SearchServiceConfiguration> _options;
        private readonly ILogger<SearchStatusService> _logger;

        public SearchStatusService(
            ISearchIndexClientWrapper searchIndex,
            ISearchIndexClientWrapper hijackIndex,
            IAuxiliaryDataCache auxiliaryDataCache,
            IOptionsSnapshot<SearchServiceConfiguration> options,
            ILogger<SearchStatusService> logger)
        {
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _hijackIndex = hijackIndex ?? throw new ArgumentNullException(nameof(hijackIndex));
            _auxiliaryDataCache = auxiliaryDataCache ?? throw new ArgumentNullException(nameof(auxiliaryDataCache));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SearchStatusResponse> GetStatusAsync(Assembly assemblyForMetadata)
        {
            var response = new SearchStatusResponse
            {
                Success = true,
            };

            response.Duration = await Measure.DurationAsync(() => PopulateResponseAsync(assemblyForMetadata, response));

            _logger.LogInformation(
                "It took {Duration} to fetch the search status. Success is {Success}.",
                response.Duration,
                response.Success);

            return response;
        }

        private async Task PopulateResponseAsync(Assembly assembly, SearchStatusResponse response)
        {
            await Task.WhenAll(
                TryAsync(
                    async () => response.SearchIndex = await GetIndexStatusAsync(_searchIndex),
                    response,
                    "warming the search index"),
                TryAsync(
                    async () => response.HijackIndex = await GetIndexStatusAsync(_hijackIndex),
                    response,
                    "warming the hijack index"),
                TryAsync(
                    async () => response.AuxiliaryFiles = await GetAuxiliaryFilesMetadataAsync(),
                    response,
                    "getting cached auxiliary data"),
                TryAsync(
                    async () => response.Server = await GetServerStatusAsync(assembly),
                    response,
                    "getting server information"));
        }

        private async Task TryAsync<T>(
            Func<Task<T>> getAsync,
            SearchStatusResponse response,
            string operation)
        {
            try
            {
                await Task.Yield();
                await getAsync();
            }
            catch (Exception ex)
            {
                response.Success = false;
                _logger.LogError(0, ex, "When getting the search status, {Operation} failed.", operation);
            }
        }

        private Task<ServerStatus> GetServerStatusAsync(Assembly assembly)
        {
            DateTimeOffset processStartTime;
            int processId;
            using (var process = Process.GetCurrentProcess())
            {
                processStartTime = process.StartTime.ToUniversalTime();
                processId = process.Id;
            }

            var serverStatus = new ServerStatus
            {
                AssemblyBuildDateUtc = GetAssemblyMetadataOrNull(assembly, "BuildDateUtc"),
                AssemblyCommitId = GetAssemblyMetadataOrNull(assembly, "CommitId"),
                AssemblyInformationalVersion = GetAssemblyInformationalVersionOrNull(assembly),
                DeploymentLabel = _options.Value.DeploymentLabel,
                MachineName = Environment.MachineName,
                InstanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"),
                ProcessDuration = DateTimeOffset.UtcNow - processStartTime,
                ProcessId = processId,
                ProcessStartTime = processStartTime,
            };

            return Task.FromResult(serverStatus);
        }

        private async Task<AuxiliaryFilesMetadata> GetAuxiliaryFilesMetadataAsync()
        {
            await _auxiliaryDataCache.EnsureInitializedAsync();
            return _auxiliaryDataCache.Get().Metadata;
        }

        private static string GetAssemblyInformationalVersionOrNull(Assembly assembly)
        {
            return assembly
                .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
                .Select(x => x.InformationalVersion)
                .FirstOrDefault();
        }

        private static string GetAssemblyMetadataOrNull(Assembly assembly, string name)
        {
            return assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(x => x.Key == name)
                .Select(x => x.Value)
                .FirstOrDefault();
        }

        private static async Task<IndexStatus> GetIndexStatusAsync(ISearchIndexClientWrapper index)
        {
            var documentCount = await index.Documents.CountAsync();
            var warmQueryDuration = await Measure.DurationAsync(() => index.Documents.SearchAsync("*", new SearchParameters()));

            return new IndexStatus
            {
                DocumentCount = documentCount,
                Name = index.IndexName,
                WarmQueryDuration = warmQueryDuration,
            };
        }
    }
}
