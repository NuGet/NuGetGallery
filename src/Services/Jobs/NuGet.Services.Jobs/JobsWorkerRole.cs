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
using NuGet.Services.Jobs.Monitoring;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.Jobs
{
    public class JobsWorkerRole : NuGetWorkerRole
    {
        protected override IEnumerable<NuGetService> GetServices(ServiceHost host)
        {
            for (int i = 0; i < (Environment.ProcessorCount - 1); i++)
            {
                yield return new JobsService(host);
            }
        }

        protected override NuGetHttpService GetManagementService(ServiceHost host)
        {
            return new JobsManagementService(host);
        }
    }
}
