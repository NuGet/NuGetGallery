// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// The basic model for a client that conforms to the NuGet protocol.
    /// </summary>
    public class PackageManagerViewModel
    {
        public PackageManagerViewModel(string name)
        {
            Name = name;
            CopyLabel = string.Format("Copy the {0} command", name);
        }

        /// <summary>
        /// The package manager's name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A unique identifier that represents this package manager.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The non-selectable prefix displayed on before the package manager's commands.
        /// </summary>
        public string CommandPrefix { get; set; }

        /// <summary>
        /// One or more strings that represent the command(s) used to install a specific package.
        /// </summary>
        public string[] InstallPackageCommands { get; set; }

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
    }

    public enum AlertLevel
    {
        None,
        Info,
        Warning
    }
}