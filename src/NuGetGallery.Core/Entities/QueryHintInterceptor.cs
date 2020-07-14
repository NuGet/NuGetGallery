// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;
using System.Diagnostics.CodeAnalysis;

namespace NuGetGallery
{
    /// <summary>
    /// A global (static) interceptor for Entity Framework queries. This is used for
    /// <see cref="IReadOnlyEntitiesContext.WithQueryHint(string)"/> to set a query hint for a short window of time on
    /// a specific entity context. Inspired by: https://stackoverflow.com/a/45170243
    /// </summary>
    public class QueryHintInterceptor : DbCommandInterceptor
    {
        [SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "The query hint is not customer input.")]
        public override void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            foreach (var dbContext in interceptionContext.DbContexts)
            {
                if (dbContext is IReadOnlyEntitiesContext entitiesContext)
                {
                    var queryHint = entitiesContext.QueryHint;
                    if (queryHint != null)
                    {
                        command.CommandText += $" OPTION ( {queryHint} )";
                    }

                    break;
                }
            }

            base.ReaderExecuting(command, interceptionContext);
        }
    }
}
