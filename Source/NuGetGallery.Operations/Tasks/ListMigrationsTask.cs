using System;
using System.Data.Entity.Migrations;
using System.IO;
using System.Reflection;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks
{
    [Command("listmigrations", "Lists migrations in the specified assembly/database combination", AltName = "lm", MaxArgs = 0)]
    public class ListMigrationsTask : MigrationsTask
    {
        protected override void ExecuteCommandCore(DbMigrator migrator)
        {
            Log.Info("Migration Status for {0} on {1}",
                     ConnectionStringBuilder.InitialCatalog,
                     ConnectionStringBuilder.DataSource);

            foreach (var migration in migrator.GetDatabaseMigrations())
            {
                Log.Info("✓ {0}", migration);
            }

            foreach (var migration in migrator.GetPendingMigrations())
            {
                Log.Info("! {0}", migration);
            }
            Log.Info("✓ = Applied, ! = Not Yet Applied");
        }
    }
}
