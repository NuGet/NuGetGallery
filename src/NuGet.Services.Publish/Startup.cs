using Microsoft.Owin;
using Microsoft.Owin.Security.ActiveDirectory;
using Owin;
using System.Configuration;
using System.Net;
using System.Threading.Tasks;

[assembly: OwinStartup(typeof(NuGet.Services.Publish.Startup))]

namespace NuGet.Services.Publish
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseErrorPage();

            app.UseWindowsAzureActiveDirectoryBearerAuthentication(
                new WindowsAzureActiveDirectoryBearerAuthenticationOptions
                {
                    Audience = ConfigurationManager.AppSettings["ida:Audience"],
                    Tenant = ConfigurationManager.AppSettings["ida:Tenant"]
                });

            app.Run(Invoke);
        }

        async Task Invoke(IOwinContext context)
        {
            switch (context.Request.Method)
            {
                case "GET":
                    await InvokeGET(context);
                    break;
                case "POST": 
                    await InvokePOST(context);
                    break;
                default:
                    await context.Response.WriteAsync("NotFound");
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
        }

        async Task InvokeGET(IOwinContext context)
        {
            switch (context.Request.Path.Value)
            {
                case "/":
                    await context.Response.WriteAsync("OK");
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    break;
                case "/test":
                    await ServiceHelpers.Test(context);
                    break;
                case "/registrations":
                    await PackageRegistrationsImpl.ListPackageRegistrations(context);
                    break;
                default:
                    await context.Response.WriteAsync("NotFound");
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
        }

        async Task InvokePOST(IOwinContext context)
        {
            switch (context.Request.Path.Value)
            {
                case "/catalog":
                    await NuGetPublishImpl.Upload(context);
                    break;
                case "/catalog/microservices":
                    PublishImpl uploader = new MicroservicesPublishImpl();
                    await uploader.Upload(context, "<unknown>");
                    break;
                default:
                    await context.Response.WriteAsync("NotFound");
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
        }
    }
}