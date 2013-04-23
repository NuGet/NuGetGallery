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
        public override void ExecuteCommand()
        {
            if (CurrentEnvironment == null)
            {
                Log.Warn("No current environment!");
            }
            else
            {
                Log.Info("Listing Environment-provided Settings for {0}", EnvironmentName);
                foreach (var pair in CurrentEnvironment.Settings)
                {
                    Log.Info(" {0} = {1}", pair.Key, pair.Value);
                }
            }
        }
    }
}
