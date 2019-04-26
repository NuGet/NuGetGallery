// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using System;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace NuGetGallery.DatabaseMigrationTools
{
    public class MigrationContextFactory : IMigrationContextFactory
    {
        private IServiceProvider _serviceProvider;
        public MigrationContextFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<IMigrationContext> CreateMigrationContext(MigrationTargetDatabaseType migrationTargetDatabaseType)
        {
            SqlConnection sqlConnection;
            switch (migrationTargetDatabaseType)
            {
                case MigrationTargetDatabaseType.GalleryDatabase:
                    sqlConnection = await _serviceProvider.GetRequiredService<ISqlConnectionFactory<GalleryDbConfiguration>>().CreateAsync();
                    return new GalleryDbMigrationContext(sqlConnection);
                case MigrationTargetDatabaseType.SupportRequestDatabase:
                    sqlConnection = await _serviceProvider.GetRequiredService<ISqlConnectionFactory<SupportRequestDbConfiguration>>().CreateAsync();
                    return new SupportRequestDbMigrationContext(sqlConnection);
                default:
                    throw new ArgumentException("Invalidate target database for migrations: "
                        + Enum.GetName(typeof(MigrationTargetDatabaseType), migrationTargetDatabaseType));
            }
        }
    }
}
