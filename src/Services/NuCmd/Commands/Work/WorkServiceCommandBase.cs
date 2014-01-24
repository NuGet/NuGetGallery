using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services;
using NuGet.Services.Work.Client;
using PowerArgs;

namespace NuCmd.Commands.Work
{
    public abstract class WorkServiceCommandBase : Command
    {
        [ArgShortcut("url")]
        [ArgDescription("The URI to the root of the work service")]
        public Uri ServiceUri { get; set; }

        [ArgShortcut("pass")]
        [ArgDescription("The admin password for the service")]
        public string Password { get; set; }

        [ArgShortcut("ice")]
        [ArgDescription("Ignore certificate errors")]
        public bool IgnoreCertErrors { get; set; }

        protected virtual async Task<WorkClient> OpenClient()
        {
            if (IgnoreCertErrors || (ServiceUri != null && String.Equals(ServiceUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)))
            {
                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return true;
                };
            }

            // Prefill values that come from the environment
            if (TargetEnvironment != null && ServiceUri == null)
            {
                ServiceUri = TargetEnvironment.GetServiceUri("work");
            }

            if (ServiceUri == null)
            {
                await Console.WriteErrorLine(Strings.ParameterRequired, "SerivceUri");
                return null;
            }
            else
            {
                // Create a client
                var httpClient = new HttpClient(
                    new ConsoleHttpTraceHandler(
                        Console,
                        new WebRequestHandler()
                        {
                            Credentials = String.IsNullOrEmpty(Password) ? null : new NetworkCredential("admin", Password)
                        }))
                        {
                            BaseAddress = ServiceUri
                        };

                return new WorkClient(httpClient);
            }
        }
    }
}
