using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using Owin;
using System;
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

            app.Run(Invoke);
        }

        public async Task Invoke(IOwinContext context)
        {
            string publisher = "<unknown>";

            switch (context.Request.Path.Value)
            {
                case "/":
                    await context.Response.WriteAsync("OK");
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    break;
                case "/catalog":
                    if (context.Request.Method.Equals("POST", StringComparison.InvariantCultureIgnoreCase))
                    {
                        await NuGetPublishImpl.Upload(context);
                    }
                    else
                    {
                        await context.Response.WriteAsync("NotFound");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                    break;
                case "/catalog/microservices":
                    if (context.Request.Method.Equals("POST", StringComparison.InvariantCultureIgnoreCase))
                    {
                        PublishImpl uploader = new MicroservicesPublishImpl();
                        await uploader.Upload(context, publisher);
                    }
                    else
                    {
                        await context.Response.WriteAsync("NotFound");
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                    break;
                default:
                    await context.Response.WriteAsync("NotFound");
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    break;
            }
        }
    }
}