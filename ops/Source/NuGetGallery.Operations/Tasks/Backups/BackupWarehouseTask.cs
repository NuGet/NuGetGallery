using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.Backups
{
    [Command("backupwarehouse", "Backs up the database", AltName = "bw", MaxArgs = 0)]
    public class BackupWarehouseTask : BackupDatabaseTask
    {
        public override void ValidateArguments()
        {
            BackupNamePrefix = BackupNamePrefix ?? "WarehouseBackup_";

            base.ValidateArguments();
        }

        protected override SqlConnectionStringBuilder SelectEnvironmentConnection(DeploymentEnvironment env)
        {
            return env.WarehouseDatabase;
        }
    }
}
