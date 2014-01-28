using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace NuGet.Services.Operations.Commands
{
    [Cmdlet(VerbsCommon.New, "NuOpsSession")]
    public class NewNuOpsSessionCommand : OpsSessionCmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(CreateSession());
        }
    }
}
