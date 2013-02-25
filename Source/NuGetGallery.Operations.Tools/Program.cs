using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using NuGet;
using NLog.Config;
using NLog.Targets;
using NLog;

namespace NuGetGallery.Operations.Tools
{
    [Export]
    class Program
    {
        private Logger _logger = LogManager.GetLogger("Program");

        public HelpCommand HelpCommand { get; set; }

        [ImportMany]
        public IEnumerable<ICommand> Commands { get; set; }

        [Import]
        public ICommandManager Manager { get; set; }

        static int Main(string[] args)
        {
            DebugHelper.WaitForDebugger(ref args);

            // Configure Logging
            ConfigureLogs();

            // Compose
            AggregateCatalog catalog = new AggregateCatalog(
                new AssemblyCatalog(typeof(Program).Assembly),
                new AssemblyCatalog(typeof(HelpCommand).Assembly));
            CompositionContainer container = new CompositionContainer(catalog);
            Program p = container.GetExportedValue<Program>();

            // Execute
            return p.Invoke(args);
        }

        public int Invoke(string[] args)
        {
            try
            {
                HelpCommand = new HelpCommand(Manager, "galops", "NuGet Gallery Operations", "https://github.com/NuGet/NuGetOperations/wiki/GalOps---Gallery-Operations-Commands");

                // Add commands
                foreach (ICommand cmd in Commands)
                {
                    Manager.RegisterCommand(cmd);
                }

                // Parse the command
                CommandLineParser parser = new CommandLineParser(Manager);
                ICommand command = parser.ParseCommandLine(args) ?? HelpCommand;

                // Fall back on help command if we failed to parse a valid command
                if (!ArgumentCountValid(command))
                {
                    string commandName = command.CommandAttribute.CommandName;
                    Console.WriteLine("{0}: invalid arguments..", commandName);
                    HelpCommand.ViewHelpForCommand(commandName);
                }
                else
                {
                    command.Execute();
                }
            }
            catch (AggregateException exception)
            {
                string message;
                Exception unwrappedEx = ExceptionUtility.Unwrap(exception);
                if (unwrappedEx == exception)
                {
                    // If the AggregateException contains more than one InnerException, it cannot be unwrapped. In which case, simply print out individual error messages
                    message = String.Join(Environment.NewLine, exception.InnerExceptions.Select(ex => ex.Message).Distinct(StringComparer.CurrentCulture));
                }
                else
                {
                    message = ExceptionUtility.Unwrap(exception).Message;
                }
                _logger.ErrorException(message, exception);
                return 1;
            }
            catch (Exception e)
            {
                var ex = ExceptionUtility.Unwrap(e);
                _logger.ErrorException(ex.Message, ex);
                return 1;
            }
            return 0;
        }

        private static void ConfigureLogs()
        {
            // Just a simple logging mechanism
            ColoredConsoleTarget consoleTarget = new ColoredConsoleTarget
            {
                Layout = "${pad:padding=4:fixedLength=true:inner=${replace:searchFor=debug:replaceWith=dbg:inner=${replace:searchFor=error:replaceWith=err:inner=${level:lowercase=true}}}}: ${message}",
                UseDefaultRowHighlightingRules = false,
                WordHighlightingRules =
                {
                    new ConsoleWordHighlightingRule("info", ConsoleOutputColor.Green, ConsoleOutputColor.NoChange),
                    new ConsoleWordHighlightingRule("err", ConsoleOutputColor.Red, ConsoleOutputColor.NoChange),
                    new ConsoleWordHighlightingRule("dbg", ConsoleOutputColor.Magenta, ConsoleOutputColor.NoChange),
                    new ConsoleWordHighlightingRule("Curated", ConsoleOutputColor.Green, ConsoleOutputColor.NoChange)
                    {
                        WholeWords = true
                    },
                    new ConsoleWordHighlightingRule("Ignored", ConsoleOutputColor.Gray, ConsoleOutputColor.NoChange)
                    {
                        WholeWords = true
                    },
                }
            };

            LoggingConfiguration config = new LoggingConfiguration();
            config.AddTarget("console", consoleTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));

            LogManager.Configuration = config;
        }

        public static bool ArgumentCountValid(ICommand command)
        {
            CommandAttribute attribute = command.CommandAttribute;
            return command.Arguments.Count >= attribute.MinArgs &&
                   command.Arguments.Count <= attribute.MaxArgs;
        }
    }
}
