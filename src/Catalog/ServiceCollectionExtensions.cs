// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDownloadsV1JsonClient(
            this IServiceCollection services,
            Func<IServiceProvider, string> urlFactory,
            Func<IServiceProvider, string> connectionStringFactory = null)
        {
            services.AddSingleton<IDownloadsV1JsonClient>(provider =>
            {
                var url = urlFactory(provider);
                var connectionString = connectionStringFactory?.Invoke(provider);

                BlobClient blobClient;
                if (!string.IsNullOrEmpty(connectionString))
                {
                    var blobUri = new BlobUriBuilder(new Uri(url));
                    blobClient = new BlobClient(connectionString, blobUri.BlobContainerName, blobUri.BlobName);
                }
                else
                {
                    var configuration = provider.GetRequiredService<IConfiguration>();
                    blobClient = new BlobClient(new Uri(url), configuration.GetTokenCredential());
                }

                var logger = provider.GetRequiredService<ILogger<DownloadsV1JsonClient>>();

                return new DownloadsV1JsonClient(blobClient, logger);
            });
            return services;
        }
    }
}
