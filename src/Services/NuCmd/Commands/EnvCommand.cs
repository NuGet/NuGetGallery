using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuCmd.Commands
{
    [Description("Displays information about the current environment")]
    public class EnvCommand : Command
    {
        protected override async Task OnExecute()
        {
            if (TargetEnvironment == null)
            {
                await Console.WriteInfoLine("No current environment");
            }
            else
            {
                await Console.WriteInfoLine("Environment: {0}", TargetEnvironment.Name);
                await Console.WriteInfoLine("Azure Subscription: {0} ({1})", TargetEnvironment.SubscriptionName, TargetEnvironment.SubscriptionId);
                await Console.WriteInfoLine("Azure Certificate: {0}", TargetEnvironment.SubscriptionCertificate.Thumbprint);
                await Console.WriteInfoLine("Service URIs:");
                foreach (var pair in TargetEnvironment.ServiceMap)
                {
                    await Console.WriteInfoLine(" {0}: {1}", pair.Key, pair.Value.AbsoluteUri);
                }
            }
        }
    }
}
