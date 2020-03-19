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
using NuGet.Services.Logging;
using NuGet.Services.Sql;

namespace NuGet.Jobs
{
    using ICoreSqlConnectionFactory = Services.Sql.ISqlConnectionFactory;

    public abstract class JobBase
    {
        private readonly EventSource _jobEventSource;

        protected JobBase()
            : this(null)
        {
        }

        protected JobBase(EventSource jobEventSource)
        {
            _jobEventSource = jobEventSource;
            SqlConnectionFactories = new Dictionary<string, ICoreSqlConnectionFactory>();
            GlobalTelemetryDimensions = new Dictionary<string, string>();
        }

        protected ILoggerFactory LoggerFactory { get; private set; }

        protected ILogger Logger { get; private set; }

        protected ApplicationInsightsConfiguration ApplicationInsightsConfiguration { get; private set; }

        /// <summary>
        /// Enables a job to define global dimensions to be tracked as part of telemetry.
        /// </summary>
        public IDictionary<string, string> GlobalTelemetryDimensions { get; private set; }

        private Dictionary<string, ICoreSqlConnectionFactory> SqlConnectionFactories { get; }

        public void SetLogger(ILoggerFactory loggerFactory, ILogger logger)
        {
            LoggerFactory = loggerFactory;
            Logger = logger;
        }

        /// <summary>
        /// Initialize the job, provided the service container and configuration.
        /// </summary>
        public abstract void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary);

        /// <summary>
        /// Run the job.
        /// </summary>
        public abstract Task Run();

        /// <summary>
        /// Test connection early to fail fast, and log connection diagnostics.
        /// </summary>
        private async Task TestConnection(string name, ICoreSqlConnectionFactory connectionFactory)
        {
            try
            {
                using (var connection = await connectionFactory.OpenAsync())
                using (var cmd = new SqlCommand("SELECT CONCAT(CURRENT_USER, '/', SYSTEM_USER)", connection))
                {
                    var result = cmd.ExecuteScalar();
                    var user = result.ToString();
                    Logger.LogInformation("Verified CreateSqlConnectionAsync({name}) connects to database {DataSource}/{InitialCatalog} as {User}",
                        name, connectionFactory.DataSource, connectionFactory.InitialCatalog, user);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(0, e, "Failed to connect to database {DataSource}/{InitialCatalog}",
                    connectionFactory.DataSource, connectionFactory.InitialCatalog);

                throw;
            }
        }

        public SqlConnectionStringBuilder GetDatabaseRegistration<T>()
            where T : IDbConfiguration
        {
            if (SqlConnectionFactories.TryGetValue(GetDatabaseKey<T>(), out var connectionFactory))
            {
                return ((AzureSqlConnectionFactory)connectionFactory).SqlConnectionStringBuilder;
            }

            return null;
        }

        /// <summary>
        /// Initializes an <see cref="ISqlConnectionFactory"/>, for use by validation jobs.
        /// </summary>
        /// <returns>ConnectionStringBuilder, used for diagnostics.</returns>
        public SqlConnectionStringBuilder RegisterDatabase<T>(
            IServiceProvider services,
            bool testConnection = true)
            where T : class, IDbConfiguration, new()
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var secretInjector = services.GetRequiredService<ISecretInjector>();
            var connectionString = services.GetRequiredService<IOptionsSnapshot<T>>().Value.ConnectionString;

            return RegisterDatabase(GetDatabaseKey<T>(), connectionString, testConnection, secretInjector);
        }

        /// <summary>
        /// Initializes an <see cref="ISqlConnectionFactory"/>, for use by non-validation jobs.
        /// </summary>
        /// <returns>ConnectionStringBuilder, used for diagnostics.</returns>
        public SqlConnectionStringBuilder RegisterDatabase(
            IServiceContainer serviceContainer,
            IDictionary<string, string> jobArgsDictionary,
            string connectionStringArgName,
            bool testConnection = true)
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

            return RegisterDatabase(connectionStringArgName, connectionString, testConnection, secretInjector);
        }

        /// <summary>
        /// Register a job database at initialization time. Each call should overwrite any existing
        /// registration because <see cref="JobRunner"/> calls <see cref="Init"/> on every iteration.
        /// </summary>
        /// <returns>ConnectionStringBuilder, used for diagnostics.</returns>
        private SqlConnectionStringBuilder RegisterDatabase(
            string name,
            string connectionString,
            bool testConnection,
            ISecretInjector secretInjector)
        {
            var connectionFactory = new AzureSqlConnectionFactory(connectionString, secretInjector, Logger);
            SqlConnectionFactories[name] = connectionFactory;

            if (testConnection)
            {
                Task.Run(() => TestConnection(name, connectionFactory)).Wait();
            }

            return connectionFactory.SqlConnectionStringBuilder;
        }

        private ICoreSqlConnectionFactory GetSqlConnectionFactory<T>()
            where T : IDbConfiguration
        {
            return GetSqlConnectionFactory(GetDatabaseKey<T>());
        }

        private ICoreSqlConnectionFactory GetSqlConnectionFactory(string name)
        {
            if (!SqlConnectionFactories.ContainsKey(name))
            {
                throw new InvalidOperationException($"Database {name} has not been registered.");
            }

            return SqlConnectionFactories[name];
        }

        private static string GetDatabaseKey<T>()
        {
            return typeof(T).Name;
        }

        /// <summary>
        /// Create a SqlConnection, for use by jobs that use an EF context.
        /// </summary>
        public Task<SqlConnection> CreateSqlConnectionAsync<T>()
            where T : IDbConfiguration
        {
            return GetSqlConnectionFactory<T>().CreateAsync();
        }

        /// <summary>
        /// Synchronous creation of a SqlConnection, for use by jobs that use an EF context.
        /// </summary>
        public SqlConnection CreateSqlConnection<T>()
            where T : IDbConfiguration
        {
            return Task.Run(() => CreateSqlConnectionAsync<T>()).Result;
        }

        /// <summary>
        /// Open a SqlConnection, for use by jobs that do NOT use an EF context.
        /// </summary>
        public Task<SqlConnection> OpenSqlConnectionAsync<T>()
            where T : IDbConfiguration
        {
            return GetSqlConnectionFactory<T>().OpenAsync();
        }

        /// <summary>
        /// Opens a SqlConnection, for use by jobs that do NOT use an EF context.
        /// </summary>
        public Task<SqlConnection> OpenSqlConnectionAsync(string connectionStringArgName)
        {
            if (string.IsNullOrEmpty(connectionStringArgName))
            {
                throw new ArgumentException("Argument cannot be null or empty.", nameof(connectionStringArgName));
            }

            return GetSqlConnectionFactory(connectionStringArgName).OpenAsync();
        }

        internal void SetApplicationInsightsConfiguration(ApplicationInsightsConfiguration applicationInsightsConfiguration)
        {
            ApplicationInsightsConfiguration = applicationInsightsConfiguration ?? throw new ArgumentNullException(nameof(applicationInsightsConfiguration));
        }
    }
}
