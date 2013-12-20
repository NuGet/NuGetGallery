using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Autofac;
using NuGet.Services.Http;
using NuGet.Services.Http.Models;
using NuGet.Services.Work.Api;
using NuGet.Services.Work.Api.Models;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace NuGet.Services.Work
{
    public class WorkManagementService : NuGetApiService
    {
        private object _apiModel = null;

        public WorkManagementService(ServiceHost host) : base("WorkManagement", host) { }

        public override void RegisterComponents(Autofac.ContainerBuilder builder)
        {
            base.RegisterComponents(builder);
            builder.RegisterModule(new JobComponentsModule());
        }

        public override Task<object> GetApiModel(NuGetApiController controller)
        {
            if (_apiModel == null)
            {
                // Multiple threads may enter, but that's OK since the resulting object should be the same
                // So we might calculate this multiple times but there's no harm there.
                var apiModel = new
                {
                    Invocations = GenerateInvocationsApis(controller),
                    Jobs = new
                    {
                        List = controller.Url.RouteUri(Routes.GetJobs),
                        Statistics = controller.Url.RouteUri(Routes.GetJobStatistics)
                    },
                    Instances = new
                    {
                        Statistics = controller.Url.RouteUri(Routes.GetInstanceStatistics)
                    },
                };
                Interlocked.Exchange(ref _apiModel, apiModel);
            }

            return Task.FromResult<object>(_apiModel);
        }

        private JObject GenerateInvocationsApis(NuGetApiController controller)
        {
            var obj = new JObject();
            foreach (InvocationListCriteria val in Enum.GetValues(typeof(InvocationListCriteria)))
            {
                obj[val.ToString().ToLowerInvariant()] = controller.Url.RouteUri(Routes.GetInvocations, new { criteria = val.ToString().ToLowerInvariant() }).AbsoluteUri;
            }
            
            // Rewrite the "active" link
            obj[InvocationListCriteria.Active.ToString().ToLowerInvariant()] = controller.Url.RouteUri(Routes.GetInvocations).AbsoluteUri;

            // Add statistics link
            obj["statistics"] = controller.Url.RouteUri(Routes.GetInvocationStatistics).AbsoluteUri;
            return obj;
        }
    }
}
