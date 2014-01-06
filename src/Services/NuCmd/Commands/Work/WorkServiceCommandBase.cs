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
        [ArgShortcut("u")]
        [ArgDescription("The URI to the root of the work service")]
        public Uri ServiceUri { get; set; }

        [ArgShortcut("p")]
        [ArgDescription("The admin password for the service")]
        public string Password { get; set; }

        protected virtual WorkClient OpenClient()
        {
            return new WorkClient(ServiceUri,
                String.IsNullOrEmpty(Password) ?
                null :
                new NetworkCredential("admin", Password));
        }
    }
}
