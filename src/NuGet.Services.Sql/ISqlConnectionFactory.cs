// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.SqlClient;
using System.Threading.Tasks;

namespace NuGet.Services.Sql
{
    public interface ISqlConnectionFactory
    {
        string ApplicationName { get; }

        string DataSource { get; }

        string InitialCatalog { get; }

        /// <summary>
        /// Attempts to create SQL connection synchronously. The call may fail (return null) if async operation is required to complete operation.
        /// </summary>
        /// <returns>False if unable to create connection (most likely secrets need refreshing).</returns>
        bool TryCreate(out SqlConnection sqlConnection);

        /// <summary>
        /// Create a connection to the SqlServer database.
        /// </summary>
        Task<SqlConnection> CreateAsync();

        /// <summary>
        /// Create and open a connection to the SqlServer database.
        /// </summary>
        Task<SqlConnection> OpenAsync();
    }
}
