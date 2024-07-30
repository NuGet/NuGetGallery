// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace ArchivePackages
{
    public class InitializationConfiguration
    {
        /// <summary>
        /// Source storage account.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Source storage container name (defaults to "packages").
        /// </summary>
        public string SourceContainerName { get; set; }

        /// <summary>
        /// Primary archive destination.
        /// </summary>
        public string PrimaryDestination { get; set; }

        /// <summary>
        /// Secondary archive destination (optional).
        /// </summary>
        public string SecondaryDestination { get; set; }

        /// <summary>
        /// Destination storage container name (defaults to "ng-backups").
        /// </summary>
        public string DestinationContainerName { get; set; }

        /// <summary>
        /// Cursor blob name (defaults to "cursor.json").
        /// </summary>
        public string CursorBlob { get; set; }
    }
}
