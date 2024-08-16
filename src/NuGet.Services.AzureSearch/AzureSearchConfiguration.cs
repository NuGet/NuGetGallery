// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.AzureSearch
{
    public class AzureSearchConfiguration
    {
        public string SearchServiceName { get; set; }
        public string SearchServiceApiKey { get; set; }

        /// <summary>
        /// If true, use the Azure SDK default credential to authenticate with Azure Search. This will authenticate
        /// as the current user based on the credential types documented here:
        /// https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential
        /// </summary>
        public bool SearchServiceUseDefaultCredential { get; set; }

        /// <summary>
        /// This is the client ID of the user managed identity that should be used when interacting with Azure Search.
        /// The client ID value can be obtained from Azure Portal. Note that this is different from the object (principal)
        /// ID.
        /// </summary>
        public string SearchServiceManagedIdentityClientId { get; set; }

        public string SearchIndexName { get; set; }
        public string HijackIndexName { get; set; }
        public string StorageConnectionString { get; set; }
        public string StorageContainer { get; set; }
        public string StoragePath { get; set; }
        public string FlatContainerBaseUrl { get; set; }
        public string FlatContainerContainerName { get; set; }
        public bool AllIconsInFlatContainer { get; set; }

        public string NormalizeStoragePath()
        {
            var storagePath = StoragePath?.Trim('/') ?? string.Empty;
            if (storagePath.Length > 0)
            {
                storagePath = storagePath + "/";
            }

            return storagePath;
        }

        public Uri ParseFlatContainerBaseUrl()
        {
            return new Uri(FlatContainerBaseUrl, UriKind.Absolute);
        }
    }
}
