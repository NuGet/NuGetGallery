using System;
using System.Data.Entity.Migrations.Infrastructure;
using NuGetGallery.Data.Migrations;

namespace NuGetGallery.Data
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class RequiresMigrationAttribute : Attribute
    {
        private string _migrationId;
        private readonly Type _migrationType;

        public string MigrationId { get { return _migrationId ?? (_migrationId = MigrationUtils.GetMigrationId(_migrationType)); } }

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
    }
}
