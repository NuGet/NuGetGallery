using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace NuGet.Services.Operations.Commands
{
    [Cmdlet(VerbsCommon.Get, "NuOpsDatacenter")]
    public class GetNuOpsDatacenter : OpsEnvironmentCmdlet
    {
        [Parameter(Position=0, Mandatory=false)]
        public int? Id { get; set; }

        protected override void ProcessRecord()
        {
            var env = GetEnvironment(required: true);
            if (Id != null)
            {
                WriteObject(env.GetDatacenter(Id.Value));
            }
            else
            {
                WriteObject(env
                    .Datacenters
                    .OrderBy(p => p.Key)
                    .Select(p => p.Value), enumerateCollection: true);
            }
        }
    }
}
