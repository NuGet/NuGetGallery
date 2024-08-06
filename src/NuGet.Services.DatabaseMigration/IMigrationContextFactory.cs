// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.Services.DatabaseMigration
{
    // <summary>
    /// A factory to create migration context for different target databases <see cref="IMigrationContext"/>.
    /// </summary>
    public interface IMigrationContextFactory
    {
        /// <summary>
        /// Create the migration context for running database migration job.
        /// </summary>
        /// <param name="migrationTargetDatabase">The target database for migration </param>
        /// <returns>The migration context</returns>
        Task<IMigrationContext> CreateMigrationContextAsync(string migrationTargetDatabase, IServiceProvider serviceProvider);
    }
}
