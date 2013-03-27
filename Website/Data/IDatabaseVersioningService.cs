using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Data.Migrations;
using NuGetGallery.Infrastructure;

namespace NuGetGallery.Data
{
    public interface IDatabaseVersioningService
    {
        /// <summary>
        /// Gets a set of migration IDs that have been applied
        /// </summary>
        ISet<string> AppliedVersions { get; }

        /// <summary>
        /// Gets a set of migration IDs that have not yet been applied
        /// </summary>
        ISet<string> PendingVersions { get; }

        /// <summary>
        /// Gets a set of migration IDs that are defined in this application
        /// </summary>
        ISet<string> AvailableVersions { get; }

        /// <summary>
        /// Gets a description of the migration with the specified id
        /// </summary>
        /// <param name="id">The id of the migration to get</param>
        DatabaseVersion GetVersion(string id);

        /// <summary>
        /// Update to the latest migration
        /// </summary>
        void UpdateToLatest();

        /// <summary>
        /// Updates to the minimum base migration (the last one that wasn't part of the more flexible model)
        /// </summary>
        void UpdateToMinimum();
    }

    public static class DatabaseVersioningServiceExtensions
    {
        public static bool HasVersion(this IDatabaseVersioningService self, string versionName)
        {
            return self.AppliedVersions.Contains(versionName);
        }

        public static bool HasVersion<TVersion>(this IDatabaseVersioningService self) where TVersion : IMigrationMetadata, new()
        {
            return HasVersion(self, MigrationUtils.GetMigrationId<TVersion>());
        }
    }
}
