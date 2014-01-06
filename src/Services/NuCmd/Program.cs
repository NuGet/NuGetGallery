using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private CommandDirectory _directory;

        public Program(IEnumerable<string> args)
        {
            _args = args;
            _console = new SystemConsole();
            _directory = new CommandDirectory();
            _directory.LoadCommands(typeof(Program).Assembly);
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

            // Find the group being referenced
            IReadOnlyDictionary<string, CommandDefinition> groupMembers;
            string commandName;
            if (_directory.Groups.TryGetValue(groupOrRootCommand, out groupMembers))
            {
                commandName = _args.FirstOrDefault();
                _args = _args.Skip(1);
            }
            else
            {
                commandName = groupOrRootCommand;
                groupOrRootCommand = null;
                groupMembers = _directory.RootCommands;
            }

            commandName = commandName ?? String.Empty;

            CommandDefinition command;
            Type commandType;
            if (!groupMembers.TryGetValue(commandName, out command))
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
            }
            else
            {
                await Dispatch(command);
            }
        }

        private async Task Dispatch(CommandDefinition definition)
        {
            ICommand cmd = null;
            Exception thrown = null;
            try
            {
                cmd = Args.Parse(definition.Type, _args.ToArray()) as ICommand;
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
                await new HelpCommand().HelpFor(_console, definition);
            }
            else if (cmd == null)
            {
                await _console.WriteErrorLine(
                    Strings.Program_CommandNotConvertible,
                    definition.Type.FullName,
                    typeof(ICommand).FullName);
            }
            else
            {
                thrown = null;
                try
                {
                    await cmd.Execute(_console, definition, _directory);
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
