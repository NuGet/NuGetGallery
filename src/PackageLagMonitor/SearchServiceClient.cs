// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGet.Services.AzureManagement;

namespace NuGet.Jobs.Montoring.PackageLag
{
    public class SearchServiceClient : ISearchServiceClient
    {
        /// <summary>
        /// To be used for <see cref="IAzureManagementAPIWrapper"/> request
        /// </summary>
        private const string ProductionSlot = "production";

        private readonly IAzureManagementAPIWrapper _azureManagementApiWrapper;
        private readonly HttpClient _httpClient;
        private readonly IOptionsSnapshot<SearchServiceConfiguration> _configuration;
        private readonly ILogger<SearchServiceClient> _logger;

        public SearchServiceClient(
            IAzureManagementAPIWrapper azureManagementApiWrapper,
            HttpClient httpClient,
            IOptionsSnapshot<SearchServiceConfiguration> configuration,
            ILogger<SearchServiceClient> logger)
        {
            _azureManagementApiWrapper = azureManagementApiWrapper;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<DateTimeOffset> GetCommitDateTimeAsync(Instance instance, CancellationToken token)
        {
            using (var diagResponse = await _httpClient.GetAsync(
                instance.DiagUrl,
                HttpCompletionOption.ResponseContentRead,
                token))
            {
                var diagContent = diagResponse.Content;
                var searchDiagResultRaw = await diagContent.ReadAsStringAsync();
                var searchDiagResultObject = JsonConvert.DeserializeObject<SearchDiagnosticResponse>(searchDiagResultRaw);

                var commitDateTime = DateTimeOffset.Parse(
                    searchDiagResultObject.CommitUserData.CommitTimeStamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal);

                return commitDateTime;
            }
        }

        public async Task<IReadOnlyList<Instance>> GetSearchEndpointsAsync(
            RegionInformation regionInformation,
            CancellationToken token)
        {
            var result = await _azureManagementApiWrapper.GetCloudServicePropertiesAsync(
                _configuration.Value.Subscription,
                regionInformation.ResourceGroup,
                regionInformation.ServiceName,
                ProductionSlot,
                token);

            var cloudService = AzureHelper.ParseCloudServiceProperties(result);

            var instances = GetInstances(cloudService.Uri, cloudService.InstanceCount, regionInformation);

            return instances;
        }

        private List<Instance> GetInstances(Uri endpointUri, int instanceCount, RegionInformation regionInformation)
        {
            var instancePortMinimum = _configuration.Value.InstancePortMinimum;

            _logger.LogInformation(
                "Testing {InstanceCount} instances, starting at port {InstancePortMinimum} for region {Region}.",
                instanceCount,
                instancePortMinimum,
                regionInformation.Region);

            return Enumerable
                .Range(0, instanceCount)
                .Select(i =>
                {
                    var diagUriBuilder = new UriBuilder(endpointUri);

                    diagUriBuilder.Scheme = "https";
                    diagUriBuilder.Port = instancePortMinimum + i;
                    diagUriBuilder.Path = "search/diag";

                    var queryBaseUriBuilder = new UriBuilder(endpointUri);

                    queryBaseUriBuilder.Scheme = "https";
                    queryBaseUriBuilder.Port = instancePortMinimum + i;
                    queryBaseUriBuilder.Path = "search/query";

                    return new Instance(
                        ProductionSlot,
                        i,
                        diagUriBuilder.Uri.ToString(),
                        queryBaseUriBuilder.Uri.ToString(),
                        regionInformation.Region);
                })
                .ToList();
        }
    }
}
