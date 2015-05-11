// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NuGetGallery.Operations
{
    public interface ICommandManager
    {
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Method would do reflection and a property would be inappropriate.")]
        IEnumerable<ICommand> GetCommands();
        ICommand GetCommand(string commandName);
        IDictionary<OptionAttribute, PropertyInfo> GetCommandOptions(ICommand command);
        void RegisterCommand(ICommand command);
    }
}
