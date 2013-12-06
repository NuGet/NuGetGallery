using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Autofac;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGet.Services.Azure
{
    public class AzureServiceHost : ServiceHost
    {
        private NuGetWorkerRole _worker;
        
        public override string Name
        {
            get { return RoleEnvironment.CurrentRoleInstance.Id; }
        }

        public AzureServiceHost(NuGetWorkerRole worker)
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            _worker = worker;
        }

        public override string GetConfigurationSetting(string fullName)
        {
            try
            {
                return RoleEnvironment.GetConfigurationSettingValue(fullName);
            }
            catch
            {
                return base.GetConfigurationSetting(fullName);
            }
        }
    }
}
