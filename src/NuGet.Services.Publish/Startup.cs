using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Owin;
using System;
using System.Configuration;
using System.IdentityModel.Tokens;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

[assembly: OwinStartup(typeof(NuGet.Services.Publish.Startup))]

namespace NuGet.Services.Publish
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            string audience = ConfigurationManager.AppSettings["ida:Audience"];
            string tenant = ConfigurationManager.AppSettings["ida:Tenant"];
            string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];

            string metadataAddress = string.Format(aadInstance, tenant) + "/federationmetadata/2007-06/federationmetadata.xml";

            app.UseWindowsAzureActiveDirectoryBearerAuthentication(new WindowsAzureActiveDirectoryBearerAuthenticationOptions
            {
                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidAudience = audience,
                    ValidateIssuer = true,
                    IssuerValidator = (string issuer, SecurityToken securityToken, TokenValidationParameters validationParameters) => { return issuer; }
                },
                Tenant = tenant,
                MetadataAddress = metadataAddress
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
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.GetDomains(context);
                        break;
                    }
                case "/checkaccess":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
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
                        await uploader.Upload(context, true, false);
                        break;
                    }
                case "/catalog/microservices":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.Upload(context, false, false);
                        break;
                    }
                case "/catalog/microservices/public":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.Upload(context, true, false);
                        break;
                    }
                case "/catalog/apiapp":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.Upload(context, false, false);
                        break;
                    }
                case "/catalog/apiapp/public":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.Upload(context, true, false);
                        break;
                    }
                case "/catalog/apiapp/hidden":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.Upload(context, false, true);
                        break;
                    }
                case "/catalog/apiapp/public/hidden":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.Upload(context, true, true);
                        break;
                    }
                case "/tenant/add":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.AddTenant(context);
                        break;
                    }
                case "/tenant/remove":
                    {
                        PublishImpl uploader = new ApiAppsPublishImpl(registrationOwnership);
                        await uploader.RemoveTenant(context);
                        break;
                    }
                case "/catalog/powershell":
                    {
                        PublishImpl uploader = new PowerShellPublishImpl(registrationOwnership);
                        await uploader.Upload(context, true);
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