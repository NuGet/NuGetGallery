// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using NuGet.Jobs;

// ReSharper disable once CheckNamespace
namespace System.Data.SqlClient
{
    public static class DapperExtensions
    {
        public static Task<IEnumerable<T>> QueryWithRetryAsync<T>(
            this SqlConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            TimeSpan? commandTimeout = null,
            CommandType? commandType = null,
            int maxRetries = SqlRetryUtility.DefaultMaxRetries)
        {
            return SqlRetryUtility.RetryReadOnlySql(
                () => connection.QueryAsync<T>(
                    sql, 
                    param, 
                    transaction, 
                    (int?)commandTimeout?.TotalSeconds, 
                    commandType),
                maxRetries);
        }

        public static Task<T> ExecuteScalarWithRetryAsync<T>(
            this SqlConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            TimeSpan? commandTimeout = null,
            CommandType? commandType = null,
            int maxRetries = SqlRetryUtility.DefaultMaxRetries)
        {
            return SqlRetryUtility.RetryReadOnlySql(
                () => connection.ExecuteScalarAsync<T>(
                    sql, 
                    param, 
                    transaction, 
                    (int?)commandTimeout?.TotalSeconds, 
                    commandType),
                maxRetries);
        }
    }
}