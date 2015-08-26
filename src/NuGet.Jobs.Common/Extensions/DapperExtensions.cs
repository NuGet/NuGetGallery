// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;

// ReSharper disable once CheckNamespace
namespace System.Data.SqlClient
{
    public static class DapperExtensions
    {
        public static Task ExecuteAsync(this SqlConnection connection, string sql, SqlTransaction transaction = null)
        {
            SqlCommand cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            if (transaction != null)
            {
                cmd.Transaction = transaction;
            }
            return cmd.ExecuteNonQueryAsync();
        }

        public static async Task<IEnumerable<T>> QueryWithRetryAsync<T>(
            this SqlConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            int maxRetries = 10,
            Action onRetry = null)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    return await connection.QueryAsync<T>(sql, param, transaction, commandTimeout, commandType);
                }
                catch (SqlException ex)
                {
                    switch (ex.Number)
                    {
                        case -2:   // Client Timeout
                        case 701:  // Out of Memory
                        case 1204: // Lock Issue
                        case 1205: // >>> Deadlock Victim
                        case 1222: // Lock Request Timeout
                        case 8645: // Timeout waiting for memory resource
                        case 8651: // Low memory condition
                            // Ignore
                            if (attempt < (maxRetries - 1))
                            {
                                if (onRetry != null)
                                {
                                    onRetry();
                                }
                            }
                            else
                            {
                                throw;
                            }
                            break;
                        default:
                            throw;
                    }
                }
            }
            throw new Exception("Unknown error! Should have thrown the final timeout!");
        }

        public static async Task<T> ExecuteScalarWithRetryAsync<T>(
            this SqlConnection connection,
            string sql,
            object param = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            int maxRetries = 10,
            Action onRetry = null)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    return await connection.ExecuteScalarAsync<T>(sql, param, transaction, commandTimeout, commandType);
                }
                catch (SqlException ex)
                {
                    switch (ex.Number)
                    {
                        case -2:   // Client Timeout
                        case 701:  // Out of Memory
                        case 1204: // Lock Issue
                        case 1205: // >>> Deadlock Victim
                        case 1222: // Lock Request Timeout
                        case 8645: // Timeout waiting for memory resource
                        case 8651: // Low memory condition
                            // Ignore
                            if (attempt < (maxRetries - 1))
                            {
                                if (onRetry != null)
                                {
                                    onRetry();
                                }
                            }
                            else
                            {
                                throw;
                            }
                            break;
                        default:
                            throw;
                    }
                }
            }
            throw new Exception("Unknown error! Should have thrown the final timeout!");
        }
    }
}
