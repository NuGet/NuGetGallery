// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.AzureSearch.Wrappers;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchStatusService : ISearchStatusService
    {
        private readonly ISearchClientWrapper _searchIndex;
        private readonly ISearchClientWrapper _hijackIndex;
        private readonly ISearchParametersBuilder _parametersBuilder;
        private readonly IAuxiliaryDataCache _auxiliaryDataCache;
        private readonly ISecretRefresher _secretRefresher;
        private readonly IOptionsSnapshot<SearchServiceConfiguration> _options;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<SearchStatusService> _logger;

        public SearchStatusService(
            ISearchClientWrapper searchIndex,
            ISearchClientWrapper hijackIndex,
            ISearchParametersBuilder parametersBuilder,
            IAuxiliaryDataCache auxiliaryDataCache,
            ISecretRefresher secretRefresher,
            IOptionsSnapshot<SearchServiceConfiguration> options,
            IAzureSearchTelemetryService telemetryService,
            ILogger<SearchStatusService> logger)
        {
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _hijackIndex = hijackIndex ?? throw new ArgumentNullException(nameof(hijackIndex));
            _parametersBuilder = parametersBuilder ?? throw new ArgumentNullException(nameof(parametersBuilder));
            _auxiliaryDataCache = auxiliaryDataCache ?? throw new ArgumentNullException(nameof(auxiliaryDataCache));
            _secretRefresher = secretRefresher ?? throw new ArgumentNullException(nameof(secretRefresher));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SearchStatusResponse> GetStatusAsync(SearchStatusOptions options, Assembly assemblyForMetadata)
        {
            var response = new SearchStatusResponse
            {
                Success = true,
            };

            response.Duration = await Measure.DurationAsync(() => PopulateResponseAsync(options, assemblyForMetadata, response));

            _telemetryService.TrackGetSearchServiceStatus(options, response.Success, response.Duration.Value);

            return response;
        }

        private async Task PopulateResponseAsync(SearchStatusOptions options, Assembly assembly, SearchStatusResponse response)
        {
            await Task.WhenAll(
                TryAsync(
                    async () =>
                    {
                        if (options.HasFlag(SearchStatusOptions.AzureSearch))
                        {
                            response.SearchIndex = await GetIndexStatusAsync(_searchIndex);
                        }
                    },
                    response,
                    "warming the search index"),
                TryAsync(
                    async () =>
                    {
                        if (options.HasFlag(SearchStatusOptions.AzureSearch))
                        {
                            response.HijackIndex = await GetIndexStatusAsync(_hijackIndex);
                        }
                    },
                    response,
                    "warming the hijack index"),
                TryAsync(
                    async () =>
                    {
                        if (options.HasFlag(SearchStatusOptions.AuxiliaryFiles))
                        {
                            response.AuxiliaryFiles = await GetAuxiliaryFilesMetadataAsync();
                        }
                    },
                    response,
                    "getting cached auxiliary data"),
                TryAsync(
                    async () =>
                    {
                        if (options.HasFlag(SearchStatusOptions.Server))
                        {
                            response.Server = await GetServerStatusAsync(assembly);
                        }
                    },
                    response,
                    "getting server information"));
        }

        private async Task TryAsync(
            Func<Task> getAsync,
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

            var lastSecretRefresh = _secretRefresher.LastRefresh;

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
                LastServiceRefreshTime = lastSecretRefresh,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
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

        private async Task<IndexStatus> GetIndexStatusAsync(ISearchClientWrapper index)
        {
            var documentCountResult = await Measure.DurationWithValueAsync(() => index.CountAsync());
            _telemetryService.TrackDocumentCountQuery(index.IndexName, documentCountResult.Value, documentCountResult.Duration);

            var lastCommitTimestampParameters = _parametersBuilder.LastCommitTimestamp();
            var lastCommitTimestampResult = await Measure.DurationWithValueAsync(() => index
                .SearchAsync<BaseMetadataDocument>("*", lastCommitTimestampParameters));
            var lastCommitTimestamp = lastCommitTimestampResult
                .Value?
                .Values?
                .FirstOrDefault()?
                .Document
                .LastCommitTimestamp;
            _telemetryService.TrackLastCommitTimestampQuery(index.IndexName, lastCommitTimestamp, lastCommitTimestampResult.Duration);

            var warmQueryDuration = await Measure.DurationAsync(() => index
                .SearchAsync<BaseMetadataDocument>("*", new SearchOptions()));
            _telemetryService.TrackWarmQuery(index.IndexName, warmQueryDuration);

            return new IndexStatus
            {
                DocumentCount = documentCountResult.Value,
                DocumentCountDuration = documentCountResult.Duration,
                Name = index.IndexName,
                WarmQueryDuration = warmQueryDuration,
                LastCommitTimestamp = lastCommitTimestamp,
                LastCommitTimestampDuration = lastCommitTimestampResult.Duration,
            };
        }
    }
}
