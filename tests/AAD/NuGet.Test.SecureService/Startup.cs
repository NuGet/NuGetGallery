using Microsoft.Owin;
using Microsoft.Owin.Security.ActiveDirectory;
using Newtonsoft.Json.Linq;
using Owin;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;

[assembly: OwinStartup(typeof(NuGet.Test.SecureService.Startup))]

namespace NuGet.Test.SecureService
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            string audience = ConfigurationManager.AppSettings["ida:Audience"];
            string tenant = ConfigurationManager.AppSettings["ida:Tenant"];
            string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];

            if (!string.IsNullOrWhiteSpace(audience))
            {
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
            }

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
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }

        async Task InvokeGET(IOwinContext context)
        {
            switch (context.Request.Path.Value)
            {
                case "/":
                    {
                        context.Response.Write("OK");
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        break;
                    }
                case "/claims":
                    {
                        JArray result = new JArray();
                        foreach (Claim claim in ClaimsPrincipal.Current.Claims)
                        {
                            JObject obj = new JObject();
                            obj.Add("type", claim.Type);
                            obj.Add("value", claim.Value);
                            result.Add(obj);
                        }

                        await context.Response.WriteAsync(result.ToString());
                        context.Response.ContentType = "application/json";
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
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
            switch (context.Request.Path.Value)
            {
                default:
                    {
                        await context.Response.WriteAsync("NotFound");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        break;
                    }
            }
        }
    }
}