// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Storage
{
    public class AzureStorageFactory : StorageFactory
    {
        BlobServiceClient _account;
        string _containerName;
        string _path;
        private Uri _differentBaseAddress = null;
        private readonly ILogger<AzureStorage> _azureStorageLogger;
        private readonly bool _initializeContainer;

        public static string PrepareConnectionString(string connectionString)
        {
            // workaround for https://github.com/Azure/azure-sdk-for-net/issues/44373
            connectionString = connectionString.Replace("SharedAccessSignature=?", "SharedAccessSignature=");

            return connectionString;
        }

        public AzureStorageFactory(
            BlobServiceClient account,
            string containerName,
            ILogger<AzureStorage> azureStorageLogger,
            string path = null,
            Uri baseAddress = null,
            bool initializeContainer = true
            )
        {
            _account = account;
            _containerName = containerName;
            _path = null;
            _azureStorageLogger = azureStorageLogger;
            _initializeContainer = initializeContainer;

            if (path != null)
            {
                _path = path.Trim('/') + '/';
            }

            _differentBaseAddress = baseAddress;

            if (baseAddress == null)
            {
                Uri blobEndpoint = new UriBuilder(account.Uri)
                {
                    Scheme = "http", // Convert base address to http. 'https' can be used for communication but is not part of the names.
                    Port = 80
                }.Uri;

                BaseAddress = new Uri(blobEndpoint, containerName + "/" + _path ?? string.Empty);
            }
            else
            {
                Uri newAddress = baseAddress;

                if (path != null)
                {
                    newAddress = new Uri(baseAddress, path + "/");
                }

                BaseAddress = newAddress;
            }
        }

        public bool CompressContent
        {
            get;
            set;
        }

        public override Storage Create(string name = null)
        {
            string path = (_path == null) ? name : _path + name;

            path = (name == null) ? (_path == null ? String.Empty : _path.Trim('/')) : path;

            Uri newBase = _differentBaseAddress;

            if (newBase != null && !string.IsNullOrEmpty(name))
            {
                newBase = new Uri(_differentBaseAddress, name + "/");
            }

            return new AzureStorage(_account, _containerName, path, newBase, _initializeContainer, _azureStorageLogger) { Verbose = Verbose, CompressContent = CompressContent };
        }
    }
}
