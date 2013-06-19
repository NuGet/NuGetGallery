using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks
{
    [Command("settings", "Show deployment settings received from the Ops console", IsSpecialPurpose = true)]
    public class ListDeploymentSettingsTask : OpsTask
    {
        [Option("Show all settings")]
        public bool All { get; set; }

        public override void ExecuteCommand()
        {
            if (CurrentEnvironment == null)
            {
                Log.Warn("No current environment!");
            }
            else if (!All)
            {
                Log.Info("Environment: {0}", EnvironmentName);
                Log.Info(" Main SQL: {0}", CurrentEnvironment.MainDatabase.DataSource);
                Log.Info(" Warehouse SQL: {0}", CurrentEnvironment.WarehouseDatabase.DataSource);
                Log.Info(" Main Storage: {0}", CurrentEnvironment.MainStorage.Credentials.AccountName);
                Log.Info(" Warehouse SQL: {0}", CurrentEnvironment.BackupStorage.Credentials.AccountName);
                Log.Info(" SQL DAC: {0}", CurrentEnvironment.SqlDacEndpoint.AbsoluteUri);
            }
            else
            {
                Log.Info("All settings for {0}", EnvironmentName);
                foreach (var pair in CurrentEnvironment.Settings)
                {
                    Log.Info("* {0} = {1}", pair.Key, pair.Value);
                }
            }
        }
    }
}
