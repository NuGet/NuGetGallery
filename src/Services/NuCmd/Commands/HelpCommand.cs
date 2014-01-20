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
            
            if (!String.IsNullOrEmpty(Group) && !String.IsNullOrEmpty(Command))
            {
                await HelpFor(Group, Command);
            }
            else if (!String.IsNullOrEmpty(Group))
            {
                // Check if there's a root command
                CommandDefinition command;
                if (Directory.RootCommands.TryGetValue(Group, out command))
                {
                    await HelpFor(command);
                }
                else
                {
                    await HelpFor(Group);
                }
            }
            else
            {
                await HelpFor(String.Empty);
            }
        }

        private async Task HelpFor(string groupName)
        {
            // Get the list of commands
            IReadOnlyDictionary<string, CommandDefinition> groupCommands = null;
            if (!String.IsNullOrEmpty(groupName) && !Directory.Groups.TryGetValue(groupName, out groupCommands))
            {
                await Console.WriteErrorLine(Strings.Help_UnknownGroup, groupName);
            }
            else
            {
                var commands = groupCommands == null ?
                    Directory.RootCommands.Values.ToList() :
                    groupCommands.Values.ToList();

                // Calculate max size of a command or group for alignment purposes
                var maxLength = commands.Max(
                    c => Math.Max((c.Group ?? String.Empty).Length, c.Name.Length));

                if (String.IsNullOrEmpty(groupName))
                {
                    // Write the groups
                    if (Directory.Groups.Any())
                    {
                        await Console.WriteHelpLine();
                        await Console.WriteHelpLine(Strings.Help_CommandGroupsHeader);
                        foreach (var group in Directory.Groups)
                        {
                            await Console.WriteHelpLine("    {0}  {1}", group.Key.PadRight(maxLength), "TODO: group descriptions");
                        }
                    }
                }

                // Write the root commands
                if (commands.Any())
                {
                    await Console.WriteHelpLine();
                    if (String.IsNullOrEmpty(groupName))
                    {
                        await Console.WriteHelpLine(Strings.Help_GlobalCommandsHeader);
                    }
                    else
                    {
                        await Console.WriteHelpLine(Strings.Help_GroupCommandsHeader, groupName);
                    }
                    foreach (var command in commands)
                    {
                        await Console.WriteHelpLine("    {0}  {1}", command.Name.PadRight(maxLength), command.Description);
                    }
                }
                await Console.WriteHelpLine();
            }
        }

        private Task HelpFor(string group, string name)
        {
            var command = Directory.GetCommand(group, name);
            if (command == null)
            {
                return Console.WriteErrorLine(Strings.Help_UnknownCommand, group, name);
            }
            else
            {
                return HelpFor(command);
            }
        }

        private Task HelpFor(CommandDefinition command)
        {
            return HelpFor(Console, command);
        }

        public virtual async Task HelpFor(IConsole console, CommandDefinition command)
        {
            await console.WriteHelpLine("nucmd {0} - {1}", 
                String.IsNullOrEmpty(command.Group) ?
                command.Name :
                (command.Group + " " + command.Name), command.Description);
            await console.WriteHelp(ArgUsage.GetStyledUsage(command.Type).ToString());
        }
    }
}
