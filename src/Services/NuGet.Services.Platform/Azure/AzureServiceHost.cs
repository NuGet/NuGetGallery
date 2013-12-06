using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autofac;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGet.Services.Azure
{
    public class AzureServiceHost : ServiceHost
    {
        private ServiceConfiguration _config = ServiceConfiguration.CreateAzure();
        private NuGetWorkerRole _worker;
        
        public override string Name
        {
            get { return RoleEnvironment.CurrentRoleInstance.Id; }
        }

        public override ServiceConfiguration Configuration
        {
            get { return _config; }
        }

        public AzureServiceHost(NuGetWorkerRole worker)
        {
            _worker = worker;
        }

        protected override void AddServices(ContainerBuilder builder)
        {
            base.AddServices(builder);

            builder.RegisterInstance(_worker);
            
            RoleEnvironment.
        }
    }
}
