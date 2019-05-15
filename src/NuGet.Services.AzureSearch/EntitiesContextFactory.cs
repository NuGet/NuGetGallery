// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGetGallery;

namespace NuGet.Services.AzureSearch
{
    public class EntitiesContextFactory : IEntitiesContextFactory
    {
        private readonly ISqlConnectionFactory<GalleryDbConfiguration> _connectionFactory;

        public EntitiesContextFactory(ISqlConnectionFactory<GalleryDbConfiguration> connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public async Task<IEntitiesContext> CreateAsync(bool readOnly)
        {
            var sqlConnection = await _connectionFactory.CreateAsync();
            return new EntitiesContext(sqlConnection, readOnly);
        }
    }
}
