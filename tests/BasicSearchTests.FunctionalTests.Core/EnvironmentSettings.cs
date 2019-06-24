// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace BasicSearchTests.FunctionalTests.Core
{
    /// <summary>
    /// This class reads the various test run settings which are set through env variable.
    /// </summary>
    public class EnvironmentSettings
    {
        private static string _searchServiceBaseurl;
        private static string _indexBaseUrl;

        /// <summary>
        /// The environment against which the (search service) test has to be run. The value would be picked from env variable.
        /// If nothing is specified, int search service is used as default.
        /// </summary>
        public static string SearchServiceBaseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_searchServiceBaseurl))
                {
                    _searchServiceBaseurl = GetEnvironmentVariable("SearchServiceUrl", "https://nuget-int-0-v2v3search.cloudapp.net/");
                }

                return _searchServiceBaseurl;
            }
        }

        /// <summary>
        /// The index base url to get the service endpoints. The value would be picked from env variable.
        /// If nothing is specified, int environment's index base url is used as default.
        /// </summary>
        public static string IndexBaseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_indexBaseUrl))
                {
                    _indexBaseUrl = GetEnvironmentVariable("IndexBaseUrl", "https://apiint.nugettest.org/v3-index/index.json");
                }

                return _indexBaseUrl;
            }
        }

        public const string ConfigurationFilePathVariableName = "ConfigurationFilePath";

        public static string ConfigurationFilePath => GetEnvironmentVariable(ConfigurationFilePathVariableName, required: true);

        private static string GetEnvironmentVariable(string key, string defaultValue = null, bool required = false)
        {
            var envVariable = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
            if (string.IsNullOrEmpty(envVariable))
            {
                envVariable = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);
            }

            if (string.IsNullOrEmpty(envVariable))
            {
                envVariable = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine);
            }

            if (string.IsNullOrEmpty(envVariable))
            {
                if (required)
                {
                    throw new InvalidOperationException($"The '{key}' environment variable must be set to run this test");
                }

                envVariable = defaultValue;
            }

            return envVariable;
        }
    }
}
