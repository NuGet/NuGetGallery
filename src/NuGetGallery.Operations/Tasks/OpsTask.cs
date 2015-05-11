// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NLog;
using NuGetGallery.Operations.Common;
using NuGetGallery.Operations.Infrastructure;

namespace NuGetGallery.Operations
{
    public abstract class OpsTask : ICommand
    {
        private const string CommandSuffix = "Task";

        private CommandAttribute _commandAttribute;
        private List<string> _arguments = new List<string>();
        private Logger _logger;
        
        public Logger Log
        {
            get { return _logger ?? (_logger = LogManager.GetLogger("task." + GetType().Name)); }
            set { _logger = value; }
        }

        public CommandAttribute CommandAttribute
        {
            get
            {
                if (_commandAttribute == null)
                {
                    _commandAttribute = GetCommandAttribute();
                }
                return _commandAttribute;
            }
        }

        public IList<string> Arguments
        {
            get { return _arguments; }
        }
        
        [Import]
        public HelpCommand HelpCommand { get; set; }

        public DeploymentEnvironment CurrentEnvironment
        {
            get
            {
                return DeploymentEnvironment.FromEnvironment();
            }
        }

        public string EnvironmentName { get { return CurrentEnvironment == null ? null : CurrentEnvironment.EnvironmentName; } }

        [Option("Gets help for this command", AltName = "?")]
        public bool Help { get; set; }

        [Option("Instead of performing any write operations, the command will just output what it WOULD do. Read operations are still performed.", AltName = "!")]
        public bool WhatIf { get; set; }

        protected internal IDbExecutorFactory DbFactory { get; set; }

        protected OpsTask()
        {
            DbFactory = new SqlDbExecutorFactory();
        }

        public void Execute()
        {
            if (!String.IsNullOrEmpty(EnvironmentName))
            {
                Log.Info("Running against {0} environment", EnvironmentName);
            }

            if (WhatIf)
            {
                Log.Info("Running in WhatIf mode");
            }
            if (Help)
            {
                HelpCommand.ViewHelpForCommand(CommandAttribute.CommandName);
            }
            else
            {
                ValidateArguments();
                ExecuteCommand();
            }
        }

        public abstract void ExecuteCommand();
        
        public virtual void ValidateArguments()
        {
        }

        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This method does quite a bit of processing.")]
        public virtual CommandAttribute GetCommandAttribute()
        {
            var attributes = GetType().GetCustomAttributes(typeof(CommandAttribute), true);
            if (attributes.Any())
            {
                return (CommandAttribute)attributes.FirstOrDefault();
            }

            // Use the command name minus the suffix if present and default description
            string name = GetType().Name;
            int idx = name.LastIndexOf(CommandSuffix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                name = name.Substring(0, idx);
            }
            if (!String.IsNullOrEmpty(name))
            {
                return new CommandAttribute(name, TaskResources.DefaultCommandDescription);
            }
            return null;
        }
    }
}
