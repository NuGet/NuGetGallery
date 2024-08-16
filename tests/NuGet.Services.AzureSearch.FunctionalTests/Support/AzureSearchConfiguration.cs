// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core;
using Newtonsoft.Json;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public class AzureSearchConfiguration
    {
        private static AzureSearchConfiguration _configuration = null;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        public TestSettingsConfiguration TestSettings { get; set; }

        [JsonProperty]
        public string Slot { get; set; }

        public string AzureSearchAppServiceUrl => "staging".Equals(Slot ?? "", StringComparison.OrdinalIgnoreCase) 
            ? TestSettings.AzureSearchAppServiceStagingUrl
            : TestSettings.AzureSearchAppServiceProductionUrl;

        public static async Task<AzureSearchConfiguration> CreateAsync()
        {
            if (_configuration != null)
            {
                return _configuration;
            }

            try
            {
                await _semaphore.WaitAsync();

                if (_configuration != null)
                {
                    return _configuration;
                }

                _configuration = CreateInternal();
            }
            finally
            {
                _semaphore.Release();
            }

            return _configuration;
        }

        public static AzureSearchConfiguration Create()
        {
            return CreateAsync().Result;
        }

        private static AzureSearchConfiguration CreateInternal()
        {
            try
            {
                var configurationFilePath = EnvironmentSettings.ConfigurationFilePath;
                var configurationString = File.ReadAllText(configurationFilePath);
                var result = JsonConvert.DeserializeObject<AzureSearchConfiguration>(configurationString);
                return result;
            }
            catch (ArgumentException ae)
            {
                throw new ArgumentException(
                    $"No configuration file was specified! Set the '{EnvironmentSettings.ConfigurationFilePathVariableName}' environment variable to the path to a JSON configuration file.",
                    ae);
            }
            catch (Exception e)
            {
                throw new ArgumentException(
                    $"Unable to load the JSON configuration file. " +
                    $"Make sure the JSON configuration file exists at the path specified by the '{EnvironmentSettings.ConfigurationFilePathVariableName}' " +
                    $"and that it is a valid JSON file containing all required configuration.",
                    e);
            }
        }

        public class TestSettingsConfiguration
        {
            [JsonProperty]
            public bool RunAzureSearchAnalysisTests { get; set; }

            [JsonProperty]
            public bool RunAzureSearchRelevancyTests { get; set; }

            [JsonProperty]
            public string AzureSearchIndexName { get; set; }

            [JsonProperty]
            public string AzureSearchIndexUrl { get; set; }

            [JsonProperty]
            public string AzureSearchIndexAdminApiKey { get; set; }

            [JsonProperty]
            public string AzureSearchAppServiceProductionUrl { get; set; }

            [JsonProperty]
            public string AzureSearchAppServiceStagingUrl { get; set; }
        }
    }
}
