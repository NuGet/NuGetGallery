using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations.Infrastructure;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace NuGetGallery.Data.Migrations
{
    public static class MigrationUtils
    {
        public static string GetMigrationId(Type migrationType)
        {
            Debug.Assert(migrationType != null);
            var metadata = Activator.CreateInstance(migrationType) as IMigrationMetadata;
            Debug.Assert(metadata != null);
            return metadata.Id;
        }
    }
}