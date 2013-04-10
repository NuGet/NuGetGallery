using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks
{
    [Command("runmigrations", "Executes migrations against a database", AltName = "rm", MaxArgs = 0)]
    public class RunMigrationsTask : MigrationsTask
    {
        [Option("The target to migrate the database to. Timestamp does not need to be specified.", AltName="m")]
        public string TargetMigration { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(TargetMigration, "TargetMigration");
        }

        protected override void ExecuteCommandCore(DbMigrator migrator)
        {
            // Find the target migration
            foreach (var migration in migrator.GetLocalMigrations())
            {
                
            }
        }
    }
}
