// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Jobs
{
    public static class SqlRetryUtility
    {
        public const int DefaultMaxRetries = 10;

        /// <summary>
        /// If a SQL query fails due to one of these exception numbers, it should always be retried.
        /// </summary>
        /// <remarks>
        /// There are many more retriable SQL exception numbers, but these are ones we've encountered in the past.
        /// If we encounter more in the future, we should add them here as well.
        /// 
        /// Note that the current implementation does not do any special handling of the SQL connection used.
        /// Some retriable SQL exceptions require reopening the connection, but none on the list currently.
        /// The implementation will break if a <see cref="Func{Task}"/> is used that does not reopen the connection and an exception is thrown that requires reopening the connection.
        /// Do not add any SQL exceptions of class 20 or higher to this list until the implementation is improved!
        /// </remarks>
        private static readonly IReadOnlyCollection<int> RetriableSqlExceptionNumbers = new[]
        {
            701, // Out of memory
            1204, // Lock issue
            1205, // Deadlock victim
            1222, // Lock request timeout
            8645, // Timeout waiting for memory resource
            8651, // Low memory condition
        };

        /// <summary>
        /// If a SQL query fails due to one of these exception numbers, it should always be retried if the query is read-only.
        /// </summary>
        /// <remarks>
        /// The exception numbers on this list that are not on <see cref="RetriableSqlExceptionNumbers"/> do not explicitly state that the query did not complete.
        /// Retrying them may cause duplicate write operations.
        /// </remarks>
        private static readonly IReadOnlyCollection<int> RetriableReadOnlySqlExceptionNumbers = RetriableSqlExceptionNumbers.Concat(new[]
        {
            -2, // Client timeout
        }).ToList();

        /// <summary>
        /// Runs <paramref name="executeSql"/> and retries it up to <paramref name="maxRetries"/> times if it fails due to a retriable error.
        /// </summary>
        public static Task RetrySql(
            Func<Task> executeSql,
            int maxRetries = DefaultMaxRetries)
        {
            return RetrySqlInternal(
                executeSql,
                RetriableSqlExceptionNumbers,
                maxRetries);
        }

        /// <summary>
        /// Runs <paramref name="executeSql"/> and retries it up to <paramref name="maxRetries"/> times if it fails due to a retriable error.
        /// </summary>
        public static Task<T> RetryReadOnlySql<T>(
            Func<Task<T>> executeSql,
            int maxRetries = DefaultMaxRetries)
        {
            return RetrySqlInternal(
                executeSql,
                RetriableReadOnlySqlExceptionNumbers,
                maxRetries);
        }

        private static T RetrySqlInternal<T>(
            Func<T> executeSql,
            IReadOnlyCollection<int> retriableExceptionNumbers,
            int maxRetries)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    return executeSql();
                }
                catch (SqlException ex) when (attempt < maxRetries - 1 && retriableExceptionNumbers.Contains(ex.Number))
                {
                    continue;
                }
            }

            // Ideally we should never get to this point, because if all iterations of the loop throw, we should rethrow the last exception encountered.
            // However, we need to have this throw here so this code will compile.
            throw new InvalidOperationException("Failed to execute SQL call in retry loop.");
        }
    }
}
