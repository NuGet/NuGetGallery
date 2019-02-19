// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class AutocompleteDatabaseQuery
    {
        private readonly DbContext _dbContext;

        public AutocompleteDatabaseQuery(IEntitiesContext entities)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            _dbContext = (DbContext)entities;
        }

        public Task<IEnumerable<string>> RunSqlQuery(string sql, params object[] sqlParameters)
        {
            return Task.FromResult(_dbContext.Database.SqlQuery<string>(sql, sqlParameters).AsEnumerable());
        }
    }
}