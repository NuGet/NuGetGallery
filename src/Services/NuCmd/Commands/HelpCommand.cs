using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PowerArgs;

namespace NuCmd.Commands
{
    [Description("Provides help information for other commands.")]
    public class HelpCommand : Command
    {
        [ArgPosition(0)]
        public string Group { get; set; }

        [ArgPosition(1)]
        public string Command { get; set; }

        protected override async Task OnExecute()
        {
            await Console.WriteHelpLine(Strings.Usage);
            await Console.WriteHelpLine();
            await Console.WriteHelpLine("The following commands are available: ");

            // Get the list of commands
            var commands = CommandDefinition.GetAllCommands();

            // Calculate max size of a command or group for alignment purposes
            var maxLength = commands.Max(
                c => Math.Max((c.Group ?? String.Empty).Length, c.Name.Length));

            if (String.IsNullOrEmpty(Group))
            {
                // Write the groups
                var groups = commands.GroupBy(c => c.Group).Where(g => g.Key != null).ToList();
                if (groups.Any())
                {
                    await Console.WriteHelpLine();
                    await Console.WriteHelpLine("Command groups. Type 'nucmd help <group>' to see a list of commands available in that group");
                    foreach (var group in groups)
                    {
                        await Console.WriteHelpLine("    {0} {1}", group.Key.PadRight(maxLength), "TODO: group descriptions");
                    }
                }
            }

            // Write the root commands
            var rootCommands = commands.Where(c => String.Equals(String).ToList();
            if (rootCommands.Any())
            {
                await Console.WriteHelpLine();
                await Console.WriteHelpLine("Global commands. Type 'nucmd help <command>' to see detailed command help information");
                foreach (var command in rootCommands)
                {
                    await Console.WriteHelpLine("    {0} {1}", command.Name.PadRight(maxLength), command.Description);
                }
            }
        }

        public virtual async Task HelpFor(Type command)
        {
            await Console.WriteHelpLine("TODO: Help for: " + command.FullName);
        }
    }
}
