using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using NuGet.Services.Operations.Model;

namespace NuGet.Services.Operations.Commands
{
    public abstract class OpsSessionCmdlet : PSCmdlet
    {
        [Parameter(Mandatory = false)]
        public string OpsDefinitionFolder { get; set; }

        [Parameter(Mandatory = false)]
        public OperationsSession OpsSession { get; set; }

        protected OperationsSession GetSession(bool required = false)
        {
            var session = GetSessionCore();
            if (required && session == null)
            {
                throw new InvalidOperationException("This command requires an ambient session, or that a session be provided using -OpsSession or -OpsDefinitionFolder");
            }
            return session;
        }

        private OperationsSession GetSessionCore()
        {
            if (OpsSession != null)
            {
                return OpsSession;
            }

            OpsSession = GetAmbientSession();
            if (OpsSession == null)
            {
                OpsSession = CreateSession();
            }
            return OpsSession;
        }

        protected OperationsSession CreateSession()
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

            return new OperationsSession(OpsDefinitionFolder);
        }

        protected OperationsSession GetAmbientSession()
        {
            return GetVariableValue(Constants.SessionVariableName) as OperationsSession;
        }

        protected void SetCurrentSession(OperationsSession session)
        {
            SessionState.PSVariable.Set(new PSVariable(Constants.SessionVariableName, session, ScopedItemOptions.AllScope));
        }
    }

    public abstract class OpsEnvironmentCmdlet : OpsSessionCmdlet
    {
        [Parameter(Mandatory=false)]
        public string EnvironmentName { get; set; }

        [Parameter(Mandatory=false)]
        public NuOpsEnvironment Environment { get; set; }

        protected NuOpsEnvironment GetEnvironment(bool required = false)
        {
            var env = GetEnvironmentCore(required);
            if (required && env == null)
            {
                throw new InvalidOperationException("This command requires an active environment, or that an environment be provided using -EnvironmentName or -Environment");
            }
            return env;
        }

        private NuOpsEnvironment GetEnvironmentCore(bool required)
        {
            if (Environment != null)
            {
                return Environment;
            }
            var session = GetSession(required);
            
            if (!String.IsNullOrEmpty(EnvironmentName))
            {
                return session.GetEnvironment(EnvironmentName);
            }
            else
            {
                return session.CurrentEnvironment;
            }
        }
    }
}
