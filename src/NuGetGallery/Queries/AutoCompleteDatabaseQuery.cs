// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class AutoCompleteDatabaseQuery
    {
        private readonly DbContext _dbContext;

        public AutoCompleteDatabaseQuery(IEntitiesContext entities)
        {
            _dbContext = (DbContext)entities;
        }

        public Task<IEnumerable<string>> RunQuery(string sql, params object[] sqlParameters)
        {
            return Task.FromResult(_dbContext.Database.SqlQuery<string>(sql, sqlParameters).AsEnumerable());
        }
    }
}