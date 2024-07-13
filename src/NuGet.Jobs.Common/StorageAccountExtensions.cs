// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Autofac;
using Autofac.Builder;
using Microsoft.Extensions.Options;
using NuGetGallery;

namespace NuGet.Jobs
{
    public static class StorageAccountHelper
    {
        public static CloudBlobClientWrapper CreateCloudBlobClient(
            string storageConnectionString,
            bool readAccessGeoRedundant = false,
            TimeSpan? requestTimeout = null)
        {
            return new CloudBlobClientWrapper(
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
            return builder.Register(c =>
            {
                var options = c.Resolve<IOptionsSnapshot<TConfiguration>>();
                string storageConnectionString = getConnectionString(options.Value);
                bool readAccessGeoRedundant = getReadAccessGeoRedundant?.Invoke(options.Value) ?? false;
                return CreateCloudBlobClient(
                    storageConnectionString,
                    readAccessGeoRedundant,
                    requestTimeout);
            });
        }
    }
}
