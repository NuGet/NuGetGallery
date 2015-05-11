// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NuGet.Common;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Export(typeof(HelpCommand))]
    [Command(typeof(CommandHelp), "help", "HelpCommandDescription", AltName = "?", MaxArgs = 1,
        UsageSummaryResourceName = "HelpCommandUsageSummary", UsageDescriptionResourceName = "HelpCommandUsageDescription",
        UsageExampleResourceName = "HelpCommandUsageExamples")]
    public class HelpCommand : OpsTask
    {
        private readonly string _commandExe;
        private readonly ICommandManager _commandManager;
        private readonly string _helpUrl;
        private readonly string _productName;

        private string CommandName
        {
            get
            {
                if (Arguments != null && Arguments.Count > 0)
                {
                    return Arguments[0];
                }
                return null;
            }
        }

        [Option(typeof(CommandHelp), "HelpCommandAll")]
        public bool All { get; set; }

        [ImportingConstructor]
        public HelpCommand(ICommandManager commandManager)
            : this(commandManager, Assembly.GetExecutingAssembly().GetName().Name, Assembly.GetExecutingAssembly().GetName().Name, CommandLineConstants.ReferencePage)
        {
        }

        [SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "3#",
            Justification = "We don't use the Url for anything besides printing, so it's ok to represent it as a string.")]
        public HelpCommand(ICommandManager commandManager, string commandExe, string productName, string helpUrl)
        {
            _commandManager = commandManager;
            _commandExe = commandExe;
            _productName = productName;
            _helpUrl = helpUrl;
        }

        public override void ExecuteCommand()
        {
            if (!String.IsNullOrEmpty(CommandName))
            {
                ViewHelpForCommand(CommandName);
            }
            else if (All)
            {
                ViewHelp(all: true);
            }
            else
            {
                ViewHelp(all: false);
            }
        }

        public void ViewHelp(bool all)
        {
            Console.WriteLine("{0} Version: {1}", _productName, GetType().Assembly.GetName().Version);
            Console.WriteLine("usage: {0} <command> [args] [options] ", _commandExe);
            Console.WriteLine("Type '{0} help <command>' for help on a specific command.", _commandExe);
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            Console.WriteLine();

            var commands = from c in _commandManager.GetCommands()
                           where all || !c.CommandAttribute.IsSpecialPurpose
                           orderby c.CommandAttribute.CommandName
                           select c.CommandAttribute;

            // Padding for printing
            int maxWidth = commands.Max(c => c.CommandName.Length + GetAltText(c.AltName).Length);

            foreach (var command in commands)
            {
                PrintCommand(maxWidth, command);
            }

            if (_helpUrl != null)
            {
                Console.WriteLine();
                Console.WriteLine("For more information, visit {0}", _helpUrl);
            }
        }

        private static void PrintCommand(int maxWidth, CommandAttribute commandAttribute)
        {
            // Write out the command name left justified with the max command's width's padding
            Console.Write(" {0, -" + maxWidth + "}   ", GetCommandText(commandAttribute));
            // Starting index of the description
            int descriptionPadding = maxWidth + 4;
            PrintJustified(descriptionPadding, commandAttribute.Description);
        }

        private static string GetCommandText(CommandAttribute commandAttribute)
        {
            return commandAttribute.CommandName + GetAltText(commandAttribute.AltName);
        }

        public void ViewHelpForCommand(string commandName)
        {
            ICommand command = _commandManager.GetCommand(commandName);
            CommandAttribute attribute = command.CommandAttribute;

            Console.WriteLine("usage: {0} {1} {2}", _commandExe, attribute.CommandName, attribute.UsageSummary);
            Console.WriteLine();

            if (!String.IsNullOrEmpty(attribute.AltName))
            {
                Console.WriteLine("alias: {0}", attribute.AltName);
                Console.WriteLine();
            }

            Console.WriteLine(attribute.Description);
            Console.WriteLine();

            if (attribute.UsageDescription != null)
            {
                const int padding = 5;
                PrintJustified(padding, attribute.UsageDescription);
                Console.WriteLine();
            }

            var options = _commandManager.GetCommandOptions(command);

            if (options.Count > 0)
            {
                Console.WriteLine("options:");
                Console.WriteLine();

                // Get the max option width. +2 for showing + against multivalued properties
                int maxOptionWidth = options.Max(o => o.Value.Name.Length) + 2;
                // Get the max altname option width
                int maxAltOptionWidth = options.Max(o => (o.Key.AltName ?? String.Empty).Length);

                foreach (var o in options)
                {
                    Console.Write(" -{0, -" + (maxOptionWidth + 2) + "}", o.Value.Name +
                        (TypeHelper.IsMultiValuedProperty(o.Value) ? " +" : String.Empty));
                    Console.Write(" {0, -" + (maxAltOptionWidth + 4) + "}", GetAltText(o.Key.AltName));

                    PrintJustified((10 + maxAltOptionWidth + maxOptionWidth), o.Key.Description);

                }

                if (_helpUrl != null)
                {
                    Console.WriteLine();
                    Console.WriteLine("For more information, visit {0}", _helpUrl);
                }

                Console.WriteLine();
            }
        }

        private void ViewHelpForAllCommands()
        {
            var commands = from c in _commandManager.GetCommands()
                           orderby c.CommandAttribute.CommandName
                           select c.CommandAttribute;
            TextInfo info = CultureInfo.CurrentCulture.TextInfo;

            foreach (var command in commands)
            {
                Console.WriteLine(info.ToTitleCase(command.CommandName) + " Command");
                ViewHelpForCommand(command.CommandName);
            }
        }

        private static string GetAltText(string altNameText)
        {
            if (String.IsNullOrEmpty(altNameText))
            {
                return String.Empty;
            }
            return String.Format(CultureInfo.CurrentCulture, " ({0})", altNameText);
        }


        private static void PrintJustified(int startIndex, string text)
        {
            PrintJustified(startIndex, text, Console.WindowWidth);
        }

        private static void PrintJustified(int startIndex, string text, int maxWidth)
        {
            if (maxWidth > startIndex)
            {
                maxWidth = maxWidth - startIndex - 1;
            }

            while (text.Length > 0)
            {
                // Trim whitespace at the beginning
                text = text.TrimStart();
                // Calculate the number of chars to print based on the width of the System.Console
                int length = Math.Min(text.Length, maxWidth);
                // Text we can print without overflowing the System.Console.
                string content = text.Substring(0, length);
                int leftPadding = startIndex + length - Console.CursorLeft;
                // Print it with the correct padding
                Console.WriteLine(content.PadLeft(leftPadding));
                // Get the next substring to be printed
                text = text.Substring(content.Length);
            }
        }
    }
}
