// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace StatusAggregator
{
    /// <summary>
    /// Helps build a storage account from configuration.
    /// Used exclusively by DI.
    /// </summary>
    public class StatusStorageConnectionBuilder
    {
        /// <summary>
        /// An identifier to use as the name of this storage account.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Describes how to access this storage account's connection string using the job's configuration.
        /// </summary>
        public Func<StatusAggregatorConfiguration, string> GetConnectionString { get; }

        public StatusStorageConnectionBuilder(
            string name,
            Func<StatusAggregatorConfiguration, string> getConnectionString)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            GetConnectionString = getConnectionString ?? throw new ArgumentNullException(nameof(getConnectionString));
        }
    }
}
