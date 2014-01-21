using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Autofac;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Azure;
using NuGet.Services.Http;
using NuGet.Services.Work.Monitoring;
using NuGet.Services.ServiceModel;
using NuGet.Services.Work.Configuration;

namespace NuGet.Services.Work
{
    public class WorkWorkerRole : NuGetWorkerRole
    {
        protected override IEnumerable<NuGetService> GetServices(ServiceHost host)
        {
            var workConfig = host.Config.GetSection<WorkConfiguration>();
            int workersPerCore = 2;
            if (workConfig != null)
            {
                workersPerCore = workConfig.WorkersPerCore;
            }

            for (int i = 0; i < (Environment.ProcessorCount * workersPerCore); i++)
            {
                yield return new WorkService(host);
            }
        }

        protected override NuGetHttpService GetManagementService(ServiceHost host)
        {
            return new WorkManagementService(host);
        }
    }
}
