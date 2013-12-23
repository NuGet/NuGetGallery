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
            if (groupOrCommand == null)
            {
                await WriteUsage(error: Strings.Program_MissingCommand);
            }
            else
            {
                _args = _args.Skip(1);
                await DispatchGroup(groupOrCommand);
            }
        }

        private Task DispatchGroup(string groupOrRootCommand)
        {
            // Convention is simple
            //  NuCmd.Commands.<CommandName>Command
            // OR:
            //  NuCmd.Commands.<Group>.<CommandName>Command

            // Try to resolve the command directly
            var commandType = typeof(Program).Assembly.GetType("NuCmd.Commands." + groupOrRootCommand + "Command", throwOnError: false, ignoreCase: true);
            if (commandType != null && typeof(ICommand).IsAssignableFrom(commandType))
            {
                return Dispatch(commandType);
            }

            // Try using the group
            var command = _args.FirstOrDefault();
            if (command == null)
            {
                return WriteUsage(error: String.Format(CultureInfo.CurrentCulture, Strings.Program_NoSuchCommand, groupOrRootCommand));
            }
            _args = _args.Skip(1);
            commandType = typeof(Program).Assembly.GetType("NuCmd.Commands." + groupOrRootCommand + "." + command + "Command", throwOnError: false, ignoreCase: true);
            if (commandType != null && typeof(ICommand).IsAssignableFrom(commandType))
            {
                return Dispatch(commandType);
            }

            // No matches!
            return WriteUsage(error: String.Format(CultureInfo.CurrentCulture, Strings.Program_NoSuchCommandInGroup, command, groupOrRootCommand));
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
            await _console.WriteHelpLine(Strings.Usage);
        }
    }
}
