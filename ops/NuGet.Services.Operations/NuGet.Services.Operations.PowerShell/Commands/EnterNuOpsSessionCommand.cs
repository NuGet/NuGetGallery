using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace NuGet.Services.Operations.Commands
{
    [Cmdlet(VerbsCommon.Enter, "NuOpsSession")]
    public class EnterNuOpsSessionCommand : OpsSessionCmdlet
    {
        protected override void ProcessRecord()
        {
            if (OpsSession == null)
            {
                OpsSession = CreateSession();
            }
            SetCurrentSession(OpsSession);
        }
    }
}
