// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.SqlClient;
using System.Threading.Tasks;
using NuGet.Jobs.Configuration;

namespace NuGet.Jobs
{
    /// <summary>
    /// A factory to create and open <see cref="SqlConnection"/>s.
    /// </summary>
    public interface ISqlConnectionFactory
    {
        /// <summary>
        /// Create an unopened SQL connection.
        /// </summary>
        /// <returns>The unopened SQL connection.</returns>
        Task<SqlConnection> CreateAsync();

        /// <summary>
        /// Create and then open a SQL connection.
        /// </summary>
        /// <returns>A task that creates and then opens a SQL connection.</returns>
        Task<SqlConnection> OpenAsync();
    }

    /// <summary>
    /// A factory to create and open <see cref="SqlConnection"/>s for a specific
    /// <see cref="TDbConfiguration"/>. This type can be used to avoid Dependency
    /// Injection key bindings.
    /// </summary>
    /// <typeparam name="TDbConfiguration">The configuration used to create the connection.</typeparam>
    public interface ISqlConnectionFactory<TDbConfiguration> : ISqlConnectionFactory
        where TDbConfiguration : IDbConfiguration
    {
    }
}
