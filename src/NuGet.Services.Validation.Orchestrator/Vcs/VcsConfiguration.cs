// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation.Vcs
{
    /// <summary>
    /// Configuration for initializing the <see cref="VcsValidator"/>.
    /// </summary>
    public class VcsConfiguration
    {
        /// <summary>
        /// The container name to use for VCS storage resources (table, queue, and blob storage).
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// The connection string to use to connect to an Azure Storage account.
        /// </summary>
        public string DataStorageAccount { get; set; }

        /// <summary>
        /// The criteria used to determine if a package should be scanned by VCS.
        /// </summary>
        public PackageCriteria PackageCriteria { get; set; } = new PackageCriteria();
    }
}
