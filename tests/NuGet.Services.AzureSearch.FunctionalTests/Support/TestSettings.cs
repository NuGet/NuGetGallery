// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BasicSearchTests.FunctionalTests.Core;
using Microsoft.Extensions.Configuration;
using NuGet.Services.Configuration;

namespace NuGet.Services.AzureSearch.FunctionalTests.Support
{
    public class TestSettings
    {
        private static TestSettings _testSettings = null;
        private static SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        private TestSettings()
        {
        }

        public bool RunAzureSearchAnalysisTests { get; set; }

        public string AzureSearchIndexName { get; set; }
        public string AzureSearchIndexUrl { get; set; }
        public string AzureSearchIndexAdminApiKey { get; set; }

        public static async Task<TestSettings> CreateAsync()
        {
            if (_testSettings != null)
            {
                return _testSettings;
            }

            try
            {
                await _semaphore.WaitAsync();

                if (_testSettings != null)
                {
                    return _testSettings;
                }

                _testSettings = CreateInternal();
            }
            finally
            {
                _semaphore.Release();
            }

            return _testSettings;
        }

        public static TestSettings Create()
        {
            return CreateAsync().Result;
        }

        private static TestSettings CreateInternal()
        {
            return CreateInternal(EnvironmentSettings.ConfigurationName);
        }

        private static TestSettings CreateInternal(string configurationName)
        {
            var result = new TestSettings();
            var builder = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Environment.CurrentDirectory, "config"))
                .AddJsonFile(configurationName + ".json", optional: false, reloadOnChange: false);

            var uninjectedConfiguration = builder.Build();
            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var secretInjector = secretReaderFactory.CreateSecretInjector(secretReaderFactory.CreateSecretReader());

            new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Environment.CurrentDirectory, "config"))
                .AddInjectedJsonFile(configurationName + ".json", secretInjector)
                .Build()
                .GetSection(nameof(TestSettings))
                .Bind(result);

            return result;
        }
    }
}
