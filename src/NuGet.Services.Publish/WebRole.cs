using System.Linq;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGet.Services.Publish
{
    public class WebRole : RoleEntryPoint
    {
        public override bool OnStart()
        {
            RoleEnvironment.Changing += RoleEnvironmentOnChanging;

            return base.OnStart();
        }

        private void RoleEnvironmentOnChanging(object sender, RoleEnvironmentChangingEventArgs eventArgs)
        {
            // If a configuration setting is changing 
            if (eventArgs.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
            {
                // Set e.Cancel to true to restart this role instance 
                eventArgs.Cancel = true;
            }
        }
    }
}