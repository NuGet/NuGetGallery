using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations.Infrastructure;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Web;

namespace NuGetGallery.Data
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class RequiresMigrationAttribute : Attribute
    {
        private string _migrationId;
        private readonly Type _migrationType;

        public string MigrationId { get { return _migrationId ?? (_migrationId = GetMigrationId(_migrationType)); } }

        public RequiresMigrationAttribute(Type migrationType)
        {
            if (migrationType == null)
            {
                throw new ArgumentNullException("migrationType");
            }
            if (!typeof(IMigrationMetadata).IsAssignableFrom(migrationType))
            {
                throw new ArgumentException(
                    String.Format(Strings.TypeIsNotAMigration, migrationType.FullName),
                    "migrationType");
            }
            _migrationType = migrationType;
        }

        public RequiresMigrationAttribute(string migrationId)
        {
            if (String.IsNullOrEmpty(migrationId))
            {
                throw new ArgumentException(String.Format(Strings.ArgumentCannotBeNullOrEmpty, "migrationId"),
                                            "migrationId");
            }
            _migrationId = migrationId;
        }

        private static string GetMigrationId(Type migrationType)
        {
            Debug.Assert(migrationType != null);
            var metadata = Activator.CreateInstance(migrationType) as IMigrationMetadata;
            Debug.Assert(metadata != null);
            return metadata.Id;
        }
    }
}
