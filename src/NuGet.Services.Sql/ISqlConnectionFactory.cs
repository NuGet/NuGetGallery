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
        /// Create and open a connection to the SqlServer database.
        /// </summary>
        Task<SqlConnection> CreateAsync();
    }
}
