// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Services.DatabaseMigration;

namespace NuGetGallery.DatabaseMigrationTools
{
    public class MigrationContextFactory : IMigrationContextFactory
    {
        private static IReadOnlyDictionary<string, Func<IServiceProvider, Task<IMigrationContext>>> _dictionary = new Dictionary<string, Func<IServiceProvider, Task<IMigrationContext>>>
        {
            {
                MigrationTargetDatabaseArgumentNames.GalleryDatabase, async(IServiceProvider serviceProvider) =>
                {
                    var sqlConnection = await serviceProvider.GetRequiredService<ISqlConnectionFactory<GalleryDbConfiguration>>().CreateAsync();
                    return new GalleryDbMigrationContext(sqlConnection);
                }
            },
            {
                MigrationTargetDatabaseArgumentNames.SupportRequestDatabase, async(IServiceProvider serviceProvider) =>
                {
                    var sqlConnection = await serviceProvider.GetRequiredService<ISqlConnectionFactory<SupportRequestDbConfiguration>>().CreateAsync();
                    return new SupportRequestDbMigrationContext(sqlConnection);
                }
            }
        };

        public async Task<IMigrationContext> CreateMigrationContextAsync(string migrationTargetDatabase, IServiceProvider serviceProvider)
        {
            Func<IServiceProvider, Task<IMigrationContext>> migrationContext;
            if (_dictionary.TryGetValue(migrationTargetDatabase, out migrationContext))
            {
                return await migrationContext(serviceProvider);
            }
            else
            {
                throw new ArgumentException($"Invalid migration target database: {migrationTargetDatabase}", nameof(migrationTargetDatabase));
            }
        }
    }
}
