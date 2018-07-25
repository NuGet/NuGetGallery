// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.Sql;

namespace NuGet.Jobs
{
    public abstract class JobBase
    {
        private readonly EventSource _jobEventSource;

        private Dictionary<string, ISqlConnectionFactory> _sqlConnectionFactories;

        protected JobBase()
            : this(null)
        {
        }

        protected JobBase(EventSource jobEventSource)
        {
            JobName = GetType().ToString();
            _jobEventSource = jobEventSource;
            _sqlConnectionFactories = new Dictionary<string, ISqlConnectionFactory>();
        }

        public string JobName { get; private set; }

        protected ILoggerFactory LoggerFactory { get; private set; }

        protected ILogger Logger { get; private set; }

        public void SetLogger(ILoggerFactory loggerFactory, ILogger logger)
        {
            LoggerFactory = loggerFactory;
            Logger = logger;
        }

        /// <summary>
        /// Test connection early to fail fast, and log connection diagnostics.
        /// </summary>
        private async Task TestConnection(ISqlConnectionFactory connectionFactory)
        {
            try
            {
                using (var connection = await connectionFactory.OpenAsync())
                using (var cmd = new SqlCommand("SELECT CONCAT(CURRENT_USER, '/', SYSTEM_USER)", connection))
                {
                    var result = cmd.ExecuteScalar();
                    var user = result.ToString();
                    Logger.LogInformation("Connected to database {DataSource}/{InitialCatalog} as {User}",
                        connectionFactory.DataSource, connectionFactory.InitialCatalog, user);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(0, e, "Failed to connect to database {DataSource}/{InitialCatalog}",
                    connectionFactory.DataSource, connectionFactory.InitialCatalog);
            }
        }

        /// <summary>
        /// Initializes an <see cref="ISqlConnectionFactory"/>, for use by validation jobs.
        /// </summary>
        /// <returns>ConnectionStringBuilder, used for diagnostics.</returns>
        public SqlConnectionStringBuilder RegisterDatabase<T>(IServiceProvider serviceProvider)
            where T : IDbConfiguration
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var secretInjector = serviceProvider.GetRequiredService<ISecretInjector>();
            var connectionString = serviceProvider.GetRequiredService<IOptionsSnapshot<T>>().Value.ConnectionString;
            var connectionFactory = new AzureSqlConnectionFactory(connectionString, secretInjector);

            return RegisterDatabase(nameof(T), connectionString, secretInjector);
        }

        /// <summary>
        /// Initializes an <see cref="ISqlConnectionFactory"/>, for use by non-validation jobs.
        /// </summary>
        /// <returns>ConnectionStringBuilder, used for diagnostics.</returns>
        public SqlConnectionStringBuilder RegisterDatabase(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary, string connectionStringArgName)
        {
            if (serviceContainer == null)
            {
                throw new ArgumentNullException(nameof(serviceContainer));
            }

            if (jobArgsDictionary == null)
            {
                throw new ArgumentNullException(nameof(jobArgsDictionary));
            }

            if (string.IsNullOrEmpty(connectionStringArgName))
            {
                throw new ArgumentException("Argument cannot be null or empty.", nameof(connectionStringArgName));
            }

            var secretInjector = (ISecretInjector)serviceContainer.GetService(typeof(ISecretInjector));
            var connectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, connectionStringArgName);

            return RegisterDatabase(connectionStringArgName, connectionString, secretInjector);
        }

        /// <summary>
        /// Register a job database at initialization time. Each call should overwrite any existing
        /// registration because <see cref="JobRunner"/> calls <see cref="Init"/> on every iteration.
        /// </summary>
        /// <returns>ConnectionStringBuilder, used for diagnostics.</returns>
        private SqlConnectionStringBuilder RegisterDatabase(string name, string connectionString, ISecretInjector secretInjector)
        {
            var connectionFactory = new AzureSqlConnectionFactory(connectionString, secretInjector, Logger);
            _sqlConnectionFactories[name] = connectionFactory;

            Task.Run(() => TestConnection(connectionFactory)).Wait();

            return connectionFactory.SqlConnectionStringBuilder;
        }

        /// <summary>
        /// Create a SqlConnection, for use by validation jobs.
        /// </summary>
        public Task<SqlConnection> CreateSqlConnectionAsync<T>()
            where T : IDbConfiguration
        {
            var name = nameof(T);
            if (!_sqlConnectionFactories.ContainsKey(name))
            {
                throw new InvalidOperationException($"Database {name} has not been registered.");
            }

            return _sqlConnectionFactories[name].CreateAsync();
        }

        /// <summary>
        /// Synchronous creation of a SqlConnection, for use by validation jobs.
        /// </summary>
        public SqlConnection CreateSqlConnection<T>()
            where T : IDbConfiguration
        {
            return Task.Run(() => CreateSqlConnectionAsync<T>()).Result;
        }

        /// <summary>
        /// Creates and opens a SqlConnection, for use by non-validation jobs.
        /// </summary>
        public Task<SqlConnection> OpenSqlConnectionAsync(string connectionStringArgName)
        {
            if (string.IsNullOrEmpty(connectionStringArgName))
            {
                throw new ArgumentException("Argument cannot be null or empty.", nameof(connectionStringArgName));
            }

            if (!_sqlConnectionFactories.ContainsKey(connectionStringArgName))
            {
                throw new InvalidOperationException($"Database {connectionStringArgName} has not been registered.");
            }

            return _sqlConnectionFactories[connectionStringArgName].OpenAsync();
        }

        public abstract void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary);

        public abstract Task Run();
    }
}
