// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class DatabaseWrapper : IDatabase
    {
        private Database _database;

        public DatabaseWrapper(Database database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public Task<int> ExecuteSqlCommandAsync(string sql, params object[] parameters)
        {
            return _database.ExecuteSqlCommandAsync(sql, parameters);
        }

        public DbContextTransaction BeginTransaction()
        {
            return _database.BeginTransaction();
        }
    }
}
