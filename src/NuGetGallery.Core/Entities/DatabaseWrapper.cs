// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using System.Data.Entity;
using System.IO;
using System.Reflection;
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

        public IDbContextTransaction BeginTransaction()
        {
            return new DbContextTransactionWrapper(_database.BeginTransaction());
        }

        /// <summary>
        /// Execute an embedded resource SQL script.
        /// </summary>
        /// <param name="name">Resource name</param>
        /// <param name="parameters">SQL parameters</param>
        /// <returns>Resulting <see cref="System.Data.SqlClient.SqlDataReader.RecordsAffected"/></returns>
        public async Task<int> ExecuteSqlResourceAsync(string name, params object[] parameters)
        {
            string sqlCommand;

            var assembly = Assembly.GetExecutingAssembly();
            using (var reader = new StreamReader(assembly.GetManifestResourceStream(name)))
            {
                sqlCommand = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrEmpty(sqlCommand))
            {
                throw new ArgumentException($"SQL resource '{name}' is empty.", nameof(name));
            }

            return await ExecuteSqlCommandAsync(sqlCommand, parameters);
        }
    }
}
