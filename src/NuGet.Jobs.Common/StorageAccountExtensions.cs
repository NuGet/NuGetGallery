// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Autofac;
using Autofac.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGetGallery;

namespace NuGet.Jobs
{
    public static class StorageAccountHelper
    {
        private const string StorageUseManagedIdentityPropertyName = "Storage_UseManagedIdentity";
        private const string StorageManagedIdentityClientIdPropertyName = "Storage_ManagedIdentityClientId";

        public static IServiceCollection ConfigureStorageMsi(
            this IServiceCollection serviceCollection,
            IConfiguration configuration,
            string useManageIdentityPropertyName = null,
            string managedIdentityClientIdPropertyName = null)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            useManageIdentityPropertyName ??= StorageUseManagedIdentityPropertyName;
            managedIdentityClientIdPropertyName ??= StorageManagedIdentityClientIdPropertyName;

            string useManagedIdentityStr = configuration[useManageIdentityPropertyName];
            string managedIdentityClientId = configuration[managedIdentityClientIdPropertyName];
            bool useManagedIdentity = false;
            if (!string.IsNullOrWhiteSpace(useManagedIdentityStr))
            {
                useManagedIdentity = bool.Parse(useManagedIdentityStr);
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

        private static CloudBlobClientWrapper CreateCloudBlobClient(
            StorageMsiConfiguration msiConfiguration,
            string storageConnectionString,
            bool readAccessGeoRedundant = false,
            TimeSpan? requestTimeout = null)
        {
            if (msiConfiguration.UseManagedIdentity)
            {
                if (string.IsNullOrWhiteSpace(msiConfiguration.ManagedIdentityClientId))
                {
                    return CloudBlobClientWrapper.UsingDefaultAzureCredential(
                        storageConnectionString,
                        readAccessGeoRedundant: readAccessGeoRedundant,
                        requestTimeout: requestTimeout);
                }
                else
                {
                    return CloudBlobClientWrapper.UsingMsi(
                        storageConnectionString,
                        msiConfiguration.ManagedIdentityClientId,
                        readAccessGeoRedundant,
                        requestTimeout);
                }
            }

            return new CloudBlobClientWrapper(
                storageConnectionString,
                readAccessGeoRedundant,
                requestTimeout);
        }
    }
}
