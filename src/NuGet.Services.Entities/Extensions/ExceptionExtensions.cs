// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;

namespace NuGet.Services.Entities
{
    public static class ExceptionExtensions
    {
        public static bool IsSqlUniqueConstraintViolation(this DataException exception)
        {
            Exception current = exception;
            while (current is not null)
            {
                if (current is SqlException sqlException)
                {
                    switch (sqlException.Number)
                    {
                        case 547:  // Constraint check violation: https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors-0-to-999
                        case 2601: // Duplicated key row error: https://learn.microsoft.com/en-us/sql/relational-databases/replication/mssql-eng002601
                        case 2627: // Unique constraint error: https://learn.microsoft.com/en-us/sql/relational-databases/replication/mssql-eng002627
                            return true;
                    }
                }

                current = current.InnerException;
            }

            return false;
        }
    }
}
