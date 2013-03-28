using System;
using System.Data.Entity.Migrations.Infrastructure;
using System.Diagnostics;

namespace NuGetGallery.Data
{
    public static class MigrationUtils
    {
        public static string GetMigrationId<TMigration>() where TMigration : IMigrationMetadata, new()
        {
            return (new TMigration()).Id;
        }

        public static string GetMigrationId(Type migrationType)
        {
            Debug.Assert(migrationType != null);
            var metadata = Activator.CreateInstance(migrationType) as IMigrationMetadata;
            Debug.Assert(metadata != null);
            return metadata.Id;
        }
    }
}