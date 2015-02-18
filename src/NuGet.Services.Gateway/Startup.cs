using Microsoft.Owin;
using Microsoft.Owin.Security.ActiveDirectory;
using Owin;
using System;
using System.Configuration;
using System.IdentityModel.Tokens;
using System.Net;
using System.Threading.Tasks;

namespace NuGet.Services.Gateway
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseErrorPage();

            //string audience = ConfigurationManager.AppSettings["ida:Audience"];
            //string tenant = ConfigurationManager.AppSettings["ida:Tenant"];

            //app.UseWindowsAzureActiveDirectoryBearerAuthentication(
            //    new WindowsAzureActiveDirectoryBearerAuthenticationOptions
            //    {
            //        TokenValidationParameters = new TokenValidationParameters { ValidAudience = audience },
            //        Tenant = tenant
            //    });

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
                //await ServiceHelpers.WriteErrorResponse(context, error, HttpStatusCode.InternalServerError);
            }
        }

        async Task InvokeGET(IOwinContext context)
        {
            await ServiceImpl.Get(context);
        }

        async Task InvokePOST(IOwinContext context)
        {
            await context.Response.WriteAsync("NotFound");
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        }
    }
}