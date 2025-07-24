// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Autofac;
using Autofac.Builder;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Services.Configuration;
using NuGet.Services.Storage;
using NuGetGallery;

namespace NuGet.Jobs
{
    public static class StorageAccountHelper
    {
        public static IServiceCollection ConfigureStorageMsi(
            this IServiceCollection serviceCollection,
            IConfiguration configuration,
            string storageUseManagedIdentityPropertyName = null,
            string storageManagedIdentityClientIdPropertyName = null,
            string localDevelopmentPropertyName = null)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            storageUseManagedIdentityPropertyName ??= Constants.StorageUseManagedIdentityPropertyName;
            storageManagedIdentityClientIdPropertyName ??= Constants.StorageManagedIdentityClientIdPropertyName;
            localDevelopmentPropertyName ??= Constants.ConfigureForLocalDevelopment;

            string useManagedIdentityStr = configuration[storageUseManagedIdentityPropertyName];
            string localDevelopmentStr = configuration[localDevelopmentPropertyName];
            bool useManagedIdentity = false;
            bool setupLocalDevelopment = false;

            string managedIdentityClientId = string.IsNullOrWhiteSpace(configuration[storageManagedIdentityClientIdPropertyName])
                                                ? configuration[Constants.ManagedIdentityClientIdKey]
                                                : configuration[storageManagedIdentityClientIdPropertyName];

            if (!string.IsNullOrWhiteSpace(useManagedIdentityStr))
            {
                useManagedIdentity = bool.Parse(useManagedIdentityStr);
            }

            if (!string.IsNullOrWhiteSpace(localDevelopmentStr))
            {
                setupLocalDevelopment = bool.Parse(localDevelopmentStr);
            }

            if (setupLocalDevelopment)
            {
                serviceCollection.AddSingleton<TokenCredential>(new DefaultAzureCredential());
            }
            else
            {
                serviceCollection.AddSingleton<TokenCredential>(new ManagedIdentityCredential(managedIdentityClientId));
            }

            return serviceCollection.Configure<StorageMsiConfiguration>(storageConfiguration =>
            {
                storageConfiguration.UseManagedIdentity = useManagedIdentity;
                storageConfiguration.ManagedIdentityClientId = managedIdentityClientId;
            });
        }

        public static CloudBlobClientWrapper CreateCloudBlobClient(
            this IServiceProvider serviceProvider,
            string storageConnectionString,
            bool readAccessGeoRedundant = false,
            TimeSpan? requestTimeout = null)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }
            if (string.IsNullOrWhiteSpace(storageConnectionString))
            {
                throw new ArgumentException($"{nameof(storageConnectionString)} cannot be null or empty.", nameof(storageConnectionString));
            }

            var msiConfiguration = serviceProvider.GetRequiredService<IOptions<StorageMsiConfiguration>>().Value;
            return CreateCloudBlobClient(
                msiConfiguration,
                storageConnectionString,
                readAccessGeoRedundant,
                requestTimeout);
        }

        public static IRegistrationBuilder<CloudBlobClientWrapper, SimpleActivatorData, SingleRegistrationStyle> RegisterStorageAccount<TConfiguration>(
            this ContainerBuilder builder,
            Func<TConfiguration, string> getConnectionString,
            Func<TConfiguration, bool> getReadAccessGeoRedundant = null,
            TimeSpan? requestTimeout = null)
            where TConfiguration : class, new()
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            if (getConnectionString == null)
            {
                throw new ArgumentNullException(nameof(getConnectionString));
            }

            return builder.Register(c =>
            {
                var options = c.Resolve<IOptionsSnapshot<TConfiguration>>();
                string storageConnectionString = getConnectionString(options.Value);
                bool readAccessGeoRedundant = getReadAccessGeoRedundant?.Invoke(options.Value) ?? false;
                var msiConfiguration = c.Resolve<IOptions<StorageMsiConfiguration>>().Value;
                return CreateCloudBlobClient(
                    msiConfiguration,
                    storageConnectionString,
                    readAccessGeoRedundant,
                    requestTimeout);
            });
        }

        public static TableServiceClient CreateTableServiceClient(
            this IServiceProvider serviceProvider,
            string storageConnectionString)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }
            if (string.IsNullOrWhiteSpace(storageConnectionString))
            {
                throw new ArgumentException($"{nameof(storageConnectionString)} cannot be null or empty.", nameof(storageConnectionString));
            }

            StorageMsiConfiguration msiConfiguration = serviceProvider.GetRequiredService<IOptions<StorageMsiConfiguration>>().Value;
            return CreateTableServiceClient(msiConfiguration, storageConnectionString);
        }

        public static IRegistrationBuilder<TableServiceClient, SimpleActivatorData, SingleRegistrationStyle> RegisterTableServiceClient<TConfiguration>(
            this ContainerBuilder builder,
            Func<TConfiguration, string> getConnectionString)
            where TConfiguration : class, new()
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            if (getConnectionString == null)
            {
                throw new ArgumentNullException(nameof(getConnectionString));
            }

            return builder.Register(c =>
            {
                IOptionsSnapshot<TConfiguration> options = c.Resolve<IOptionsSnapshot<TConfiguration>>();
                string storageConnectionString = getConnectionString(options.Value);
                StorageMsiConfiguration msiConfiguration = c.Resolve<IOptions<StorageMsiConfiguration>>().Value;
                return CreateTableServiceClient(msiConfiguration, storageConnectionString);
            });
        }

        private static CloudBlobClientWrapper CreateCloudBlobClient(
            StorageMsiConfiguration msiConfiguration,
            string storageConnectionString,
            bool readAccessGeoRedundant = false,
            TimeSpan? requestTimeout = null)
        {
            if (msiConfiguration.UseManagedIdentity)
            {
#if DEBUG
                return CloudBlobClientWrapper.UsingDefaultAzureCredential(
                    storageConnectionString,
                    readAccessGeoRedundant: readAccessGeoRedundant,
                    requestTimeout: requestTimeout);
#else
                return CloudBlobClientWrapper.UsingMsi(
                    storageConnectionString,
                    msiConfiguration.ManagedIdentityClientId,
                    readAccessGeoRedundant,
                    requestTimeout);
#endif
            }

            return new CloudBlobClientWrapper(
                storageConnectionString,
                readAccessGeoRedundant,
                requestTimeout);
        }

        public static BlobServiceClient CreateBlobServiceClient(
            StorageMsiConfiguration storageMsiConfiguration,
            string storageConnectionString,
            TimeSpan? requestTimeout = null)
        {
            BlobClientOptions blobClientOptions = new BlobClientOptions();
            if (requestTimeout.HasValue)
            {
                blobClientOptions.Retry.NetworkTimeout = requestTimeout.Value;
            }

            if (storageMsiConfiguration.UseManagedIdentity)
            {
                Uri blobEndpointUri = AzureStorage.GetPrimaryServiceUri(storageConnectionString);

                if (string.IsNullOrWhiteSpace(storageMsiConfiguration.ManagedIdentityClientId))
                {
                    // 1. Using MSI with DefaultAzureCredential (local debugging)
                    return new BlobServiceClient(
                        blobEndpointUri,
                        new DefaultAzureCredential(),
                        blobClientOptions);
                }
                else
                {
                    // 2. Using MSI with ClientId
                    return new BlobServiceClient(
                        blobEndpointUri,
                        new ManagedIdentityCredential(storageMsiConfiguration.ManagedIdentityClientId),
                        blobClientOptions);
                }
            }
            else
            {
                // 3. Using SAS token
                // workaround for https://github.com/Azure/azure-sdk-for-net/issues/44373
                var connectionString = storageConnectionString.Replace("SharedAccessSignature=?", "SharedAccessSignature=");

                return new BlobServiceClient(connectionString, blobClientOptions);
            }
        }

        public static BlobServiceClientFactory CreateBlobServiceClientFactory(
            StorageMsiConfiguration storageMsiConfiguration,
            string storageConnectionString)
        {
            if (storageMsiConfiguration.UseManagedIdentity)
            {
                Uri blobEndpointUri = AzureStorage.GetPrimaryServiceUri(storageConnectionString);

                if (string.IsNullOrWhiteSpace(storageMsiConfiguration.ManagedIdentityClientId))
                {
                    // 1. Using MSI with DefaultAzureCredential (local debugging)
                    return new BlobServiceClientFactory(
                        blobEndpointUri,
                        new DefaultAzureCredential());
                }
                else
                {
                    // 2. Using MSI with ClientId
                    return new BlobServiceClientFactory(
                        blobEndpointUri,
                        new ManagedIdentityCredential(storageMsiConfiguration.ManagedIdentityClientId));
                }
            }
            else
            {
                // 3. Using SAS token
                // workaround for https://github.com/Azure/azure-sdk-for-net/issues/44373
                var connectionString = storageConnectionString.Replace("SharedAccessSignature=?", "SharedAccessSignature=");

                return new BlobServiceClientFactory(connectionString);
            }
        }

        public static TableServiceClient CreateTableServiceClient(
            StorageMsiConfiguration msiConfiguration,
            string tableStorageConnectionString)
        {
            if (msiConfiguration.UseManagedIdentity)
            {
                Uri tableEndpointUri = new Uri(tableStorageConnectionString);

                if (string.IsNullOrWhiteSpace(msiConfiguration.ManagedIdentityClientId))
                {
                    return new TableServiceClient(tableEndpointUri, new DefaultAzureCredential());
                }
                else
                {
                    return new TableServiceClient(tableEndpointUri, new ManagedIdentityCredential(msiConfiguration.ManagedIdentityClientId));
                }
            }

            // workaround for https://github.com/Azure/azure-sdk-for-net/issues/44373
            var connectionString = tableStorageConnectionString.Replace("SharedAccessSignature=?", "SharedAccessSignature=");
            return new TableServiceClient(connectionString);
        }
    }
}
