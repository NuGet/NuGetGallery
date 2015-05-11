// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using NuBot.Infrastructure;
using NuGet;
using NLog.Config;
using NLog.Targets;
using NLog;
using System.Diagnostics.CodeAnalysis;

namespace NuGetGallery.Operations.Tools
{
    [Export]
    public class Program
    {
        private Logger _logger = LogManager.GetLogger("task.Program");

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
            var catalog = new AggregateCatalog(
                new AssemblyCatalog(typeof(Program).Assembly),
                new AssemblyCatalog(typeof(HelpCommand).Assembly));
            var container = new CompositionContainer(catalog);
            var p = container.GetExportedValue<Program>();

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
                var parser = new CommandLineParser(Manager);
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
                _logger.Error("{0}: {1}", unwrappedEx.GetType().Name, message);
                _logger.Error(" Stack Trace: " + unwrappedEx.StackTrace);
                return 1;
            }
            catch (Exception e)
            {
                var ex = ExceptionUtility.Unwrap(e);
                _logger.Error("{0}: {1}", ex.GetType().Name, ex.Message);
                _logger.Error(" Stack Trace: " + ex.StackTrace);
                return 1;
            }
            return 0;
        }

        private static void ConfigureLogs()
        {
            // Just a simple logging mechanism
            var consoleTarget = new SnazzyConsoleTarget()
            {
                Layout = "${message}"
            };
            var joblogTarget = new SnazzyConsoleTarget()
            {
                Layout = "[${date:format=yyyy-MM-dd} ${date:format=HH}:${date:format=mm}:${date:format=ss}] ${message}"
            };

            var config = new LoggingConfiguration();
            config.AddTarget("task", consoleTarget);
            config.AddTarget("joblog", joblogTarget);
            config.LoggingRules.Add(new LoggingRule("joblog.*", LogLevel.Trace, joblogTarget));
            config.LoggingRules.Add(new LoggingRule("task.*", LogLevel.Trace, consoleTarget));

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
