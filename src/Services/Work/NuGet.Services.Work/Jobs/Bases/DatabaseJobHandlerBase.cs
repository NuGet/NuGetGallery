using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using NuGet.Services.Configuration;
using NuGet.Services.Work.Jobs.Models;

namespace NuGet.Services.Work.Jobs
{
    public abstract class DatabaseJobHandlerBase<T> : AsyncJobHandler<T>
        where T : EventSource
    {
        /// <summary>
        /// The target server, in the form of a known SQL Server (primary, warehouse, etc.)
        /// </summary>
        public KnownSqlServer TargetServer { get; set; }

        /// <summary>
        /// The name of the database to back up
        /// </summary>
        public string TargetDatabaseName { get; set; }

        /// <summary>
        /// A connection string to the database to be backed up. The user credentials must
        /// also be valid for connecting to the master database on that server.
        /// </summary>
        public SqlConnectionStringBuilder TargetDatabaseConnection { get; set; }

        protected ConfigurationHub Config { get; set; }

        protected DatabaseJobHandlerBase(ConfigurationHub config)
        {
            Config = config;
        }

        protected virtual SqlConnectionStringBuilder GetConnectionString(bool admin)
        {
            var connection = TargetDatabaseConnection;
            if (connection == null)
            {
                connection = Config.Sql
                    .GetConnectionString(TargetServer, admin);
                if (!String.IsNullOrEmpty(TargetDatabaseName))
                {
                    connection = connection.ChangeDatabase(TargetDatabaseName);
                }
            }
            return connection;
        }

        protected internal virtual async Task<Database> GetDatabase(SqlConnection connection, string name)
        {
            return (await connection.QueryAsync<Database>(@"
                SELECT name, database_id, create_date, state
                FROM sys.databases
                WHERE name = @name
            ", new { name })).FirstOrDefault();
        }

        protected internal virtual Task<IEnumerable<Database>> GetDatabases(SqlConnection connection)
        {
            return connection.QueryAsync<Database>(@"
                SELECT name, database_id, create_date, state
                FROM sys.databases
            ");
        }

        protected internal virtual Task<IEnumerable<Database>> GetDatabases(SqlConnection connection, DatabaseState state)
        {
            return connection.QueryAsync<Database>(@"
                SELECT name, database_id, create_date, state
                FROM sys.databases
                WHERE state = @state
            ", new { state = (int)state });
        }
    }
}
