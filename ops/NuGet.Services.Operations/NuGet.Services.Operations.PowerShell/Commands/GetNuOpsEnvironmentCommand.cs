using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using NuGet.Services.Operations.Model;

namespace NuGet.Services.Operations.Commands
{
    [Cmdlet(VerbsCommon.Get, "NuOpsEnvironment")]
    public class GetNuOpsEnvironmentCommand : OpsSessionCmdlet
    {
        [Parameter(Position=0, Mandatory=false, ValueFromPipeline=true)]
        public string Name { get; set; }

        [Parameter(Mandatory=false)]
        public bool ListAvailable { get; set; }

        protected override void ProcessRecord()
        {
            var session = GetSession();

            if (!String.IsNullOrEmpty(Name) && !ListAvailable)
            {
                // Get the current environment
                WriteObject(session.CurrentEnvironment);
            }
            else
            {
                IEnumerable<NuOpsEnvironment> envs = session.Environments.Values;
                if (!String.IsNullOrEmpty(Name))
                {
                    var pattern = new WildcardPattern(Name, WildcardOptions.IgnoreCase);
                    envs = envs.Where(e => pattern.IsMatch(e.Name));
                }
                WriteObject(envs, enumerateCollection: true);
            }
        }
    }
}
