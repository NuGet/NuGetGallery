using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Work.Client;
using PowerArgs;

namespace NuCmd.Commands.Work
{
    public abstract class WorkServiceCommandBase : Command
    {
        [ArgRequired()]
        [ArgShortcut("url")]
        [ArgDescription("The URI to the root of the work service")]
        public Uri ServiceUri { get; set; }

        [ArgShortcut("pass")]
        [ArgDescription("The admin password for the service")]
        public string Password { get; set; }

        [ArgShortcut("ice")]
        [ArgDescription("Ignore certificate errors")]
        public bool IgnoreCertErrors { get; set; }

        protected virtual WorkClient OpenClient()
        {
            if (IgnoreCertErrors)
            {
                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return true;
                };
            }

            return new WorkClient(ServiceUri,
                String.IsNullOrEmpty(Password) ?
                null :
                new NetworkCredential("admin", Password));
        }
    }
}
