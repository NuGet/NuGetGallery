// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NuGetGallery
{
    /// <summary>
    /// The basic model for a client that conforms to the NuGet protocol.
    /// </summary>
    public class PackageManagerViewModel
    {
        public PackageManagerViewModel(
            string id,
            string name,
            params InstallPackageCommand[] installPackageCommands)
            : this(id, name, initialismExplanation: null, installPackageCommands)
        {
        }

        public PackageManagerViewModel(
            string id,
            string name,
            string initialismExplanation,
            params InstallPackageCommand[] installPackageCommands)
        {
            Id = id;
            Name = name;
            InitialismExplanation = initialismExplanation;
            CopyLabel = string.Format(CultureInfo.InvariantCulture, "Copy the {0} command", name);
            int index = 0;
            InstallPackageCommands = installPackageCommands.ToDictionary(
                _ => string.Format(CultureInfo.InvariantCulture, "{0}-{1:0000}", Id, ++index),
                value => value,
                StringComparer.OrdinalIgnoreCase
                );
        }

        /// <summary>
        /// The package manager's name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// If <see cref="Name"/> is an initialism this property contains the explanation for it shown in a tooltip.
        /// If null or empty string, <see cref="Name"/> is used instead.
        /// </summary>
        public string InitialismExplanation { get; } = null;

        /// <summary>
        /// A unique identifier that represents this package manager.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The non-selectable prefix displayed on before the package manager's commands.
        /// </summary>
        public string CommandPrefix { get; set; }

        /// <summary>
        /// One or more strings that represent the command(s) used to install a specific package.
        /// </summary>
        public Dictionary<string, InstallPackageCommand> InstallPackageCommands { get; }

        /// <summary>
        /// The alert message that contains clarifications about the command/scenario
        /// </summary>
        public string AlertMessage { get; set; }

        /// <summary>
        /// The level with which the above message will be displayed.
        /// </summary>
        public AlertLevel AlertLevel { get; set; }

        /// <summary>
        /// The label for the copy button.
        /// </summary>
        public string CopyLabel { get; set; }

        /// <summary>
        /// Whether the copy button is enabled or not.
        /// </summary>
        public bool CopyEnabled { get; set; } = true;

        public class InstallPackageCommand
        {
            public InstallPackageCommand(string command) : this(string.Empty, command)
            {
            }

            public  InstallPackageCommand(string header, string command)
            {
                HasHeader = !string.IsNullOrWhiteSpace(header);
                Header = header;
                Command = command;
            }

            public bool HasHeader { get; }
            public string Header { get; }
            public string Command { get; }
        }
    }

    public enum AlertLevel
    {
        None,
        Info,
        Warning
    }
}
