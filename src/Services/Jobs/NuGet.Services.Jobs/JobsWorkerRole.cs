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

namespace NuGet.Services.Jobs
{
    public class JobsWorkerRole : NuGetWorkerRole
    {
        protected override IEnumerable<Type> GetServices()
        {
            // TODO: Register multiple copies per processor?
            yield return typeof(JobsService);
            
            yield return typeof(JobsManagementService);
        }
    }
}
