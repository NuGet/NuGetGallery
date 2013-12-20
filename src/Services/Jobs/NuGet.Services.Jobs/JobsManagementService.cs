using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Autofac;
using NuGet.Services.Http;
using NuGet.Services.Http.Models;
using NuGet.Services.Jobs.Api;
using NuGet.Services.Jobs.Api.Models;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;

namespace NuGet.Services.Jobs
{
    public class JobsManagementService : NuGetApiService
    {
        public JobsManagementService(ServiceHost host) : base("JobsManagement", host) { }

        public override void RegisterComponents(Autofac.ContainerBuilder builder)
        {
            base.RegisterComponents(builder);
            builder.RegisterModule(new JobComponentsModule());
        }

        public override Task<object> GetApiModel(NuGetApiController controller)
        {
            return Task.FromResult<object>(new
            {
                Invocations = new {
                    All = controller.Url.RouteUri(Routes.GetInvocations, new { criteria = InvocationListCriteria.All }),
                    Active = controller.Url.RouteUri(Routes.GetInvocations),
                    Completed = controller.Url.RouteUri(Routes.GetInvocations, new { criteria = InvocationListCriteria.Completed }),
                    Hidden = controller.Url.RouteUri(Routes.GetInvocations, new { criteria = InvocationListCriteria.Hidden }),
                    Pending = controller.Url.RouteUri(Routes.GetInvocations, new { criteria = InvocationListCriteria.Pending }),
                    Suspended = controller.Url.RouteUri(Routes.GetInvocations, new { criteria = InvocationListCriteria.Suspended })
                },
                Jobs = controller.Url.RouteUri(Routes.GetJobs)
            });
        }
    }
}
