using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGet.Services.Azure
{
    public class AzureServiceHost : NuGetServiceHost
    {
        private ServiceConfiguration _config = ServiceConfiguration.CreateAzure();
        
        public override string Name
        {
            get { return RoleEnvironment.CurrentRoleInstance.Id; }
        }

        public override ServiceConfiguration Configuration
        {
            get { return _config; }
        }
    }
}
