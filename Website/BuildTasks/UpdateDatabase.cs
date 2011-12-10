using System.Data.Entity.Migrations;
using Microsoft.Build.Utilities;
using NuGetGallery.Migrations;

namespace NuGetGallery
{
    public class UpdateDatabase : Task
    {
        public override bool Execute()
        {
            var dbMigrator = new DbMigrator(new Settings());
            dbMigrator.Update(TargetMigration);
            return true;
        }

        public string TargetMigration { get; set; }
    }
}
