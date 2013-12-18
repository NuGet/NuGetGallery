using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Http;
using NuGet.Services.Http.Models;
using NuGet.Services.Jobs.Api.Models;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.Jobs
{
    public class JobsManagementService : NuGetApiService
    {
        public JobsManagementService(ServiceHost host) : base("JobsManagement", host) { }

        public override Task<object> Describe()
        {
            return Task.FromResult<object>(new JobsManagementServiceDescriptionModel());
        }
    }
}
