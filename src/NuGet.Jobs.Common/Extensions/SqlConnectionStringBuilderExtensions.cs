// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace System.Data.SqlClient
{
    public static class SqlConnectionStringBuilderExtensions
    {
        public static Task<SqlConnection> ConnectTo(this SqlConnectionStringBuilder self)
        {
            return ConnectTo(self.ConnectionString);
        }

        private static async Task<SqlConnection> ConnectTo(string connection)
        {
            var c = new SqlConnection(connection);
            await c.OpenAsync().ConfigureAwait(continueOnCapturedContext: false);
            return c;
        }
    }
}