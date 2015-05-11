// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Export(typeof(ICommandManager))]
    public class CommandManager : ICommandManager
    {
        private readonly IList<ICommand> _commands = new List<ICommand>();

        public IEnumerable<ICommand> GetCommands()
        {
            return _commands;
        }

        public ICommand GetCommand(string commandName)
        {
            if (String.IsNullOrEmpty(commandName))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, commandName);
            }

            IEnumerable<ICommand> results = from command in _commands
                                            where command.CommandAttribute.CommandName.StartsWith(commandName, StringComparison.OrdinalIgnoreCase) ||
                                            (command.CommandAttribute.AltName ?? String.Empty).StartsWith(commandName, StringComparison.OrdinalIgnoreCase)
                                            select command;

            if (!results.Any())
            {
                throw new CommandLineException(TaskResources.UnknownCommandError, commandName);
            }

            var matchedCommand = results.First();
            if (results.Skip(1).Any())
            {
                // Were there more than one results found?
                matchedCommand = results.FirstOrDefault(c => c.CommandAttribute.CommandName.Equals(commandName, StringComparison.OrdinalIgnoreCase)
                    || commandName.Equals(c.CommandAttribute.AltName, StringComparison.OrdinalIgnoreCase));

                if (matchedCommand == null)
                {
                    // No exact match was found and the result returned multiple prefixes.
                    throw new CommandLineException(String.Format(CultureInfo.CurrentCulture, TaskResources.AmbiguousCommand, commandName,
                        String.Join(" ", from c in results select c.CommandAttribute.CommandName)));
                }
            }
            return matchedCommand;
        }

        public IDictionary<OptionAttribute, PropertyInfo> GetCommandOptions(ICommand command)
        {
            var result = new Dictionary<OptionAttribute, PropertyInfo>();

            foreach (PropertyInfo propInfo in command.GetType().GetProperties())
            {
                foreach (OptionAttribute attr in propInfo.GetCustomAttributes(typeof(OptionAttribute), inherit: true))
                {
                    if (!propInfo.CanWrite && !TypeHelper.IsMultiValuedProperty(propInfo))
                    {
                        // If the property has neither a setter nor is of a type that can be cast to ICollection<> then there's no way to assign 
                        // values to it. In this case throw.
                        throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                            TaskResources.OptionInvalidWithoutSetter, command.GetType().FullName + "." + propInfo.Name));
                    }
                    result.Add(attr, propInfo);
                }
            }

            return result;
        }

        public void RegisterCommand(ICommand command)
        {
            var attrib = command.CommandAttribute;
            if (attrib != null)
            {
                _commands.Add(command);
            }
        }
    }
}
