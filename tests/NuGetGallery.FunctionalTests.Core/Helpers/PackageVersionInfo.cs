// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGetGallery.FunctionalTests.Helpers
{
    public class PackageVersionInfo
    {
        public string Version { get; }
        public bool Listed { get; }
        public string ApiKey { get; }
        /// <summary>
        /// A task that completes when this package has finished being processed.
        /// </summary>
        public Task ReadyTask { get; }

        public PackageVersionInfo(string version, bool listed, Task uploadTask)
        {
            Version = version;
            Listed = listed;
            ReadyTask = uploadTask;
        }

        public bool HasApiKeyWithSameOwner(string apiKey)
        {
            return MapApiKeyToOwner(ApiKey) == MapApiKeyToOwner(apiKey);
        }

        private static string MapApiKeyToOwner(string apiKey)
        {
            if (apiKey == null ||
                apiKey == EnvironmentSettings.TestAccountApiKey ||
                apiKey == EnvironmentSettings.TestAccountApiKey_Push ||
                apiKey == EnvironmentSettings.TestAccountApiKey_PushVersion ||
                apiKey == EnvironmentSettings.TestAccountApiKey_Unlist)
            {
                return EnvironmentSettings.TestAccountName;
            }
            else if (apiKey == EnvironmentSettings.TestOrganizationAdminAccountApiKey)
            {
                return EnvironmentSettings.TestOrganizationAdminAccountName;
            }
            else if (apiKey == EnvironmentSettings.TestOrganizationCollaboratorAccountApiKey)
            {
                return EnvironmentSettings.TestOrganizationCollaboratorAccountName;
            }

            throw new ArgumentOutOfRangeException(nameof(apiKey));
        }
    }
}
