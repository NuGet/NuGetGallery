using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;

namespace NuGet.Services.Operations.Commands
{
    public abstract class OpsSessionCmdlet : PSCmdlet
    {
        [Parameter(Mandatory = false)]
        public string OpsDefinitionFolder { get; set; }

        [Parameter(Mandatory = false)]
        public OperationsSession OpsSession { get; set; }

        public OperationsSession GetSession()
        {
            if (OpsSession != null)
            {
                return OpsSession;
            }

            OpsSession = GetVariableValue(Constants.SessionVariableName) as OperationsSession;
            if (OpsSession == null)
            {
                // Use default definition file if present
                if (String.IsNullOrEmpty(OpsDefinitionFolder))
                {
                    string opsSource = Environment.GetEnvironmentVariable(Constants.OpsDefinitionEnvironmentVariable);
                    if (String.IsNullOrEmpty(opsSource))
                    {
                        throw new ArgumentException("OpsDefinitionFolder must be specified or provided in the NUOPS_DEFINITION environment variable", "OpsDefinitionFolder");
                    }
                    OpsDefinitionFolder = opsSource;
                }

                ProviderInfo provider;
                OpsDefinitionFolder = GetResolvedProviderPathFromPSPath(OpsDefinitionFolder, out provider).FirstOrDefault();
                if (!String.Equals(provider.Name, "FileSystem", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("OpsDefinitionFolder must be a file system path", "OpsDefinitionFolder");
                }

                OpsSession = new OperationsSession(OpsDefinitionFolder);
            }
            return OpsSession;
        }
    }
}
