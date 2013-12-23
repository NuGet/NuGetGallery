using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuCmd.Commands
{
    public class HelpCommand : Command
    {
        protected override async Task OnExecute()
        {
            await Console.WriteHelpLine("TODO");
        }

        public virtual async Task HelpFor(Type command)
        {
            await Console.WriteHelpLine("TODO: Help for: " + command.FullName);
        }
    }
}
