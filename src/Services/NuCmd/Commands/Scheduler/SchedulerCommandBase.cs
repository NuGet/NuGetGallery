using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Scheduler;

namespace NuCmd.Commands.Scheduler
{
    public abstract class SchedulerCommandBase : Command
    {
        protected SubscriptionCloudCredentials Credentials { get; set; }

        protected override async Task<bool> LoadContext(IConsole console, CommandDefinition definition, CommandDirectory directory)
        {
            await base.LoadContext(console, definition, directory);

            if (TargetEnvironment == null || TargetEnvironment.SubscriptionCertificate == null || String.IsNullOrEmpty(TargetEnvironment.SubscriptionId))
            {
                await Console.WriteErrorLine(Strings.Command_RequiresManagementCert);
                return false;
            }
            Credentials = new CertificateCloudCredentials(
                TargetEnvironment.SubscriptionId,
                TargetEnvironment.SubscriptionCertificate);
            return true;
        }
    }
}
