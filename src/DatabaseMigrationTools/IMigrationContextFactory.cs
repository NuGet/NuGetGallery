// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGetGallery.DatabaseMigrationTools
{
    // <summary>
    /// A factory to create migration context for different target databases <see cref="IMigrationContext"/>.
    /// </summary>
    public interface IMigrationContextFactory
    {
        /// <summary>
        /// Create the migration context for running database migration job.
        /// </summary>
        /// <param name="targetDatabseType">The target database for migration <see cref="MigrationTargetDatabaseType"/></param>
        /// <returns>The migration context</returns>
        Task<IMigrationContext> CreateMigrationContext(MigrationTargetDatabaseType migrationTargetDatabaseType);
    }
}
