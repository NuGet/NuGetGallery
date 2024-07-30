// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;

namespace NuGet.Jobs.Validation
{
    public static class ExceptionExtensions
    {
        /// <summary>
        /// The SQL Server error code for when a unique contraint is violated.
        /// </summary>
        private const int UniqueConstraintViolationErrorCode = 2627;

        /// <summary>
        /// Check whether a <see cref="DbUpdateException"/> is due to a SQL unique constraint violation.
        /// </summary>
        /// <param name="exception">The exception to inspect.</param>
        /// <returns>Whether the exception was caused to SQL unique constraint violation.</returns>
        public static bool IsUniqueConstraintViolationException(this DbUpdateException exception)
        {
            var sqlException = exception.GetBaseException() as SqlException;

            if (sqlException != null)
            {
                return sqlException.Errors.OfType<SqlError>().Any(error => error.Number == UniqueConstraintViolationErrorCode);
            }

            return false;
        }
    }
}