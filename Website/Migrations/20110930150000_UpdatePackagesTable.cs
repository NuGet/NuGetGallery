using System.Data;
using Migrator.Framework;

namespace NuGetGallery.Data.Migrations {
    [Migration(20110930150000)]
    public class UpdatePackagesTableMigration : Migration {
        public override void Up() {
            if (Database.ColumnExists("Packages", "FlattenedDependencies"))
            {
                var flattenedDependenciesColumn = Database.GetColumnByName("Packages", "FlattenedDependencies");
                flattenedDependenciesColumn.Size = 4000;
                Database.ChangeColumn("Packages", flattenedDependenciesColumn);
            }
        }

        public override void Down() {
            if (Database.ColumnExists("Packages", "FlattenedDependencies"))
            {
                var flattenedDependenciesColumn = Database.GetColumnByName("Packages", "FlattenedDependencies");
                flattenedDependenciesColumn.Size = 255;
                Database.ChangeColumn("Packages", flattenedDependenciesColumn);
            }
        }
    }
}