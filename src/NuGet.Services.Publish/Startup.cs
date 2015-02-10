using Microsoft.Owin;
using Microsoft.Owin.Security.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Owin;
using System;
using System.Configuration;
using System.IdentityModel.Tokens;
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

            string audience = ConfigurationManager.AppSettings["ida:Audience"];
            string tenant = ConfigurationManager.AppSettings["ida:Tenant"];

            app.UseWindowsAzureActiveDirectoryBearerAuthentication(
                new WindowsAzureActiveDirectoryBearerAuthenticationOptions
                {
                    TokenValidationParameters = new TokenValidationParameters { ValidAudience = audience },
                    Tenant = tenant
                });

            app.Run(Invoke);
        }

        async Task Invoke(IOwinContext context)
        {
            string error = null;

            try
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
            catch (Exception e)
            {
                error = e.Message;
            }

            if (error != null)
            {
                await ServiceHelpers.WriteErrorResponse(context, error, HttpStatusCode.InternalServerError);
            }
        }

        async Task InvokeGET(IOwinContext context)
        {
            IRegistrationOwnership registrationOwnership = CreateRegistrationOwnership(context);

            switch (context.Request.Path.Value)
            {
                case "/":
                    {
                        await context.Response.WriteAsync("OK");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        break;
                    }
                case "/domains":
                    {
                        PublishImpl uploader = new MicroservicesPublishImpl(registrationOwnership);
                        await uploader.GetDomains(context);
                        break;
                    }
                case "/checkaccess":
                    {
                        PublishImpl uploader = new MicroservicesPublishImpl(registrationOwnership);
                        await uploader.CheckAccess(context);
                        break;
                    }
                default:
                    {
                        await context.Response.WriteAsync("NotFound");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        break;
                    }
            }
        }

        async Task InvokePOST(IOwinContext context)
        {
            IRegistrationOwnership registrationOwnership = CreateRegistrationOwnership(context);

            switch (context.Request.Path.Value)
            {
                case "/catalog":
                    {
                        await NuGetPublishImpl.Upload(context);
                        break;
                    }
                case "/catalog/nuspec":
                    {
                        PublishImpl uploader = new NuSpecJsonPublishImpl(registrationOwnership);
                        await uploader.Upload(context);
                        break;
                    }
                case "/catalog/microservices":
                    {
                        PublishImpl uploader = new MicroservicesPublishImpl(registrationOwnership);
                        await uploader.Upload(context);
                        break;
                    }
                case "/tenant/add":
                    {
                        PublishImpl uploader = new MicroservicesPublishImpl(registrationOwnership);
                        await uploader.AddTenant(context);
                        break;
                    }
                case "/tenant/remove":
                    {
                        PublishImpl uploader = new MicroservicesPublishImpl(registrationOwnership);
                        await uploader.RemoveTenant(context);
                        break;
                    }
                default:
                    {
                        await context.Response.WriteAsync("NotFound");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        break;
                    }
            }
        }

        IRegistrationOwnership CreateRegistrationOwnership(IOwinContext context)
        {
            //return new AzureADRegistrationOwnership(context);

            string storagePrimary = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Primary");
            string storageContainerOwnership = System.Configuration.ConfigurationManager.AppSettings.Get("Storage.Container.Ownership") ?? "ownership";

            CloudStorageAccount account = CloudStorageAccount.Parse(storagePrimary);

            return new StorageRegistrationOwnership(context, account, storageContainerOwnership);
        }
    }
}