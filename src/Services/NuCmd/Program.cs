using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuCmd.Commands;
using PowerArgs;

namespace NuCmd
{
    class Program
    {
        public IConsole _console;
        private IEnumerable<string> _args;

        public Program(IEnumerable<string> args)
        {
            _args = args;
            _console = new SystemConsole();
        }

        static void Main(string[] args)
        {
#if DEBUG
            if (args.Length > 0 && args[0] == "dbg")
            {
                Debugger.Launch();
                args = args.Skip(1).ToArray();
            }
            if (args.Length > 0 && args[0] == "dbgw")
            {
                Console.Error.WriteLine("Waiting for debugger to attach...");
                args = args.Skip(1).ToArray();
                SpinWait.SpinUntil(() => Debugger.IsAttached);
            }
#endif
            new Program(args).Run().Wait();
        }

        private async Task Run()
        {
            await _console.WriteTraceLine("NuCmd v{0}", typeof(Program).Assembly.GetName().Version);

            // Get the command group
            var groupOrCommand = _args.FirstOrDefault();
            
            _args = _args.Skip(1);
            await DispatchGroup(groupOrCommand ?? "help");
        }

        private async Task DispatchGroup(string groupOrRootCommand)
        {
            // Commands are classes with the following naming convention
            //  NuCmd.Commands.<CommandName>Command
            // OR:
            //  NuCmd.Commands.<Group>.<CommandName>Command

            // Get all the commands by group
            var groups = CommandDefinition
                .GetAllCommands()
                .GroupBy(c => c.Group ?? String.Empty)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            // Find the group being referenced
            Dictionary<string, CommandDefinition> groupMembers;
            string commandName;
            if (groups.TryGetValue(groupOrRootCommand, out groupMembers))
            {
                commandName = _args.FirstOrDefault();
                _args = _args.Skip(1);
            }
            else
            {
                commandName = groupOrRootCommand;
                groupOrRootCommand = null;
                groupMembers = groups.ContainsKey(String.Empty) ?
                    groups[String.Empty] :
                    new Dictionary<string, CommandDefinition>();
            }

            if (commandName == null)
            {
                _args = Enumerable.Concat(
                    String.IsNullOrEmpty(groupOrRootCommand) ? 
                        Enumerable.Empty<string>() :
                        new[] { groupOrRootCommand },
                    _args);
                await Dispatch(typeof(HelpCommand));
            }
            else
            {
                CommandDefinition command;
                Type commandType;
                if (groupMembers.TryGetValue(commandName, out command))
                {
                    commandType = command.Type;
                }
                else
                {
                    if (String.IsNullOrEmpty(groupOrRootCommand))
                    {
                        await _console.WriteErrorLine(String.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Program_NoSuchCommand,
                            commandName));
                    }
                    else
                    {
                        await _console.WriteErrorLine(String.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Program_NoSuchCommandInGroup,
                            commandName,
                            groupOrRootCommand));
                    }
                    commandType = typeof(HelpCommand);
                }
                await Dispatch(commandType);
            }

        }

        private async Task Dispatch(Type commandType)
        {
            ICommand cmd = null;
            Exception thrown = null;
            try
            {
                cmd = Args.Parse(commandType, _args.ToArray()) as ICommand;
            }
            catch (AggregateException aex)
            {
                thrown = aex.InnerException;
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
            if (thrown != null)
            {
                await _console.WriteErrorLine(thrown.Message);
            }
            else if (cmd == null)
            {
                await _console.WriteErrorLine(
                    Strings.Program_CommandNotConvertible,
                    commandType.FullName,
                    typeof(ICommand).FullName);
            }
            else
            {
                thrown = null;
                try
                {
                    await cmd.Execute(_console);
                }
                catch (AggregateException aex)
                {
                    thrown = aex.InnerException;
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }
                if (thrown != null)
                {
                    await _console.WriteErrorLine(thrown.Message);
                }
            }
        }

        private async Task WriteUsage(string error)
        {
            await _console.WriteErrorLine(error);
        }
    }
}
