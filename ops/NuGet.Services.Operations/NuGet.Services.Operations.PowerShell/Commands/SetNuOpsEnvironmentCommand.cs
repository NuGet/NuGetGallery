using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace NuGet.Services.Operations.Commands
{
    [Cmdlet(VerbsCommon.Set, "NuOpsEnvironment")]
    public class SetNuOpsEnvironmentCommand : OpsSessionCmdlet
    {
        [Parameter(Position=0, Mandatory=true)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            var session = GetSession(required: true);
            session.SetCurrentEnvironment(Name);
        }
    }
}
