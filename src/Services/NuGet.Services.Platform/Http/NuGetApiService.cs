using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Autofac;
using Autofac.Integration.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NuGet.Services.Client;
using NuGet.Services.Composition;
using NuGet.Services.Http.Authentication;
using NuGet.Services.Http.Controllers;
using NuGet.Services.Http.Models;
using NuGet.Services.ServiceModel;
using Owin;

namespace NuGet.Services.Http
{
    public abstract class NuGetApiService : NuGetHttpService
    {
        public NuGetApiService(string name, ServiceHost host) : base(name, host) { }

        protected override void Configure(IAppBuilder app)
        {
            var config = Container.Resolve<HttpConfiguration>();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(Container);
            if (!String.IsNullOrEmpty(Configuration.Http.AdminKey))
            {
                app.UseAdminKeyAuthentication(new AdminKeyAuthenticationOptions()
                {
                    Key = Configuration.Http.AdminKey,
                    GrantedRole = Roles.Admin
                });
            }
            app.UseWebApi(config);
        }

        public override void RegisterComponents(Autofac.ContainerBuilder builder)
        {
            base.RegisterComponents(builder);
            
            builder.RegisterInstance(this).As<NuGetApiService>();

            var config = ConfigureWebApi();
            builder.RegisterInstance(config).AsSelf();

            builder
                .RegisterApiControllers(GetControllerAssemblies().ToArray())
                .OnActivated(e =>
                {
                    var nugetController = e.Instance as NuGetApiController;
                    if (nugetController != null)
                    {
                        nugetController.Host = e.Context.Resolve<ServiceHost>();
                        nugetController.Service = e.Context.Resolve<NuGetApiService>();
                        nugetController.Container = e.Context.Resolve<IComponentContainer>();
                    }
                })
                .InstancePerApiRequest();

            //builder.RegisterWebApiFilterProvider(config);
            //builder.RegisterWebApiModelBinderProvider();
        }

        protected virtual IEnumerable<Assembly> GetControllerAssemblies()
        {
            yield return typeof(NuGetApiService).Assembly;
            
            if (GetType().Assembly != typeof(NuGetApiService).Assembly)
            {
                yield return GetType().Assembly;
            }
        }

        protected virtual HttpConfiguration ConfigureWebApi()
        {
            var config = new HttpConfiguration();

            config.Formatters.Clear();
            config.Formatters.Add(JsonFormat.Formatter);

            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.LocalOnly;
            config.Filters.Add(new RecordApiExceptionFilter());

            // Use Attribute routing
            config.MapHttpAttributeRoutes();

            return config;
        }

        public abstract Task<object> GetApiModel(NuGetApiController controller, IPrincipal requestor);
    }
}
