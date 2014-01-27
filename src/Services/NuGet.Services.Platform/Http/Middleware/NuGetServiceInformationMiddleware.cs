using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Owin;
using Newtonsoft.Json;
using NuGet.Services.Client;
using NuGet.Services.Http.Models;
using NuGet.Services.ServiceModel;
using Owin;

namespace NuGet.Services.Http.Middleware
{
    public class NuGetServiceInformationMiddleware : OwinMiddleware
    {
        private static PathString _infoPath = new PathString("/_info");
        private static PathString _infoServicePath = new PathString("/_info/services");
        private static PathString _authPath = new PathString("/_auth");
        
        public ServiceHost Host { get; private set; }
        
        public NuGetServiceInformationMiddleware(OwinMiddleware next, ServiceHost host)
            : base(next)
        {
            Host = host;
        }

        public override Task Invoke(IOwinContext context)
        {
            // Check if we should handle the request
            var path = context.Request.Path;
            if (!path.HasValue || String.Equals(path.Value, "/", StringComparison.Ordinal))
            {
                // Root!
                return HandleRootRequest(context);
            }
            else if (path.StartsWithSegments(_authPath))
            {
                // Force admin key authentication
                if (context.Authentication.User == null)
                {
                    context.Authentication.Challenge();
                }
                else
                {
                    context.Response.Redirect(GetBaseUri(context).AbsoluteUri);
                }
                return Task.FromResult<object>(null);
            }
            else if (path.StartsWithSegments(_infoPath))
            {
                // Force admin key authentication
                if (context.Authentication.User == null || !context.Authentication.User.IsInRole(Roles.Admin))
                {
                    context.Authentication.Challenge();
                    return Task.FromResult<object>(null);
                }
                else
                {
                    // Info request
                    return HandleInfoRequest(context);
                }
            }
            else
            {
                // We don't handle this, let the services take it.
                return Next.Invoke(context);
            }
        }

        private Task HandleRootRequest(IOwinContext context)
        {
            // Generate an API Description
            var baseUri = GetBaseUri(context);
            var api = new ApiDescription(baseUri, Host.HttpServiceInstances);

            if (context.Authentication.User != null && context.Authentication.User.IsInRole(Roles.Admin))
            {
                api.Services.Add("_info", MakeAbsolute("_info", context));
                api.Host = Host.Description.ServiceHostName.ToString();
            }

            return WriteJson(context, api);
        }

        private Task HandleInfoRequest(IOwinContext context)
        {
            if (context.Request.Path.StartsWithSegments(_infoServicePath))
            {
                return HandleServiceInfoRequest(context);
            }
            else
            {
                var allAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToDictionary(
                    a => a.GetName().Name,
                    a => new {
                        a.FullName,
                        a.GetName().Name,
                        a.GetName().Version,
                        AssemblyInfo = a.GetAssemblyInfo()
                    });
                var nugetAssemblies = allAssemblies
                    .Where(pair => pair.Key.StartsWith("NuGet"))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);

                var proc = Process.GetCurrentProcess();
                var infoObject = new
                {
                    Host = Host.Description.ServiceHostName.ToString(),
                    Machine = Host.Description.MachineName,
                    Services = Host.Services.ToDictionary(
                        pair => pair.Key,
                        pair => MakeAbsolute("_info/services/" + pair.Key.ToLowerInvariant(), context)),
                    Process = new
                    {
                        Name = proc.ProcessName,
                        CPUSeconds = proc.TotalProcessorTime.TotalSeconds,
                        VirtualMemorySize = proc.VirtualMemorySize64,
                        WorkingSet = proc.WorkingSet64,
                        PagedMemorySize = proc.PagedMemorySize64,
                        NonpagedSystemMemorySize = proc.NonpagedSystemMemorySize64,
                        Threads = proc.Threads.Count,
                    },
                    NuGetAssemblies = nugetAssemblies,
                    Assemblies = allAssemblies
                };
                return WriteJson(context, infoObject);
            }
        }

        private static readonly Regex PathRegex = new Regex("^/_info/services/(?<service>[^/]*)$");
        private async Task HandleServiceInfoRequest(IOwinContext context)
        {
            var match = PathRegex.Match(context.Request.Path.Value);
            if(!match.Success) {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            else {
                string serviceName = match.Groups["service"].Value;
                var service = Host.GetInstance(serviceName);
                var infoObject = new {
                    Name = service.Name.ToString(),
                    service.LastHeartbeat,
                    Status = await service.GetCurrentStatus()
                };
                await WriteJson(context, infoObject);
            }
        }

        private static Uri GetBaseUri(IOwinContext context)
        {
            var baseUri = new UriBuilder(context.Request.Uri)
            {
                Path = context.Request.PathBase.Value
            }.Uri;
            return baseUri;
        }

        private static Uri MakeAbsolute(string relativeUrl, IOwinContext context)
        {
            return new Uri(GetBaseUri(context), relativeUrl);
        }

        private static async Task WriteJson(IOwinContext context, object value)
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(await JsonFormat.SerializeAsync(value));
        }
    }

    public static class NuGetServiceInformationMiddlewareExtensions
    {
        public static IAppBuilder UseNuGetServiceInformation(this IAppBuilder self, ServiceHost host)
        {
            return self.Use<NuGetServiceInformationMiddleware>(host);
        }
    }
}
