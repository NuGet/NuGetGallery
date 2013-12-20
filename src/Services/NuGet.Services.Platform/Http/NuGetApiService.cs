using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Autofac;
using Autofac.Integration.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using NuGet.Services.Composition;
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

            builder.RegisterWebApiFilterProvider(config);
            builder.RegisterWebApiModelBinderProvider();
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

            var serializerSettings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateParseHandling = DateParseHandling.DateTimeOffset,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                Formatting = Formatting.Indented,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Error,
                TypeNameHandling = TypeNameHandling.None
            };
            serializerSettings.Converters.Add(new StringEnumConverter());

            var formatter = new JsonMediaTypeFormatter()
            {
                SerializerSettings = serializerSettings
            };

            formatter.SupportedMediaTypes.Clear();
            formatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/json"));

            config.Formatters.Clear();
            config.Formatters.Add(formatter);

            config.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.LocalOnly;

            // Use Attribute routing
            config.MapHttpAttributeRoutes();

            return config;
        }

        public abstract Task<object> GetApiModel(NuGetApiController controller);
    }
}
