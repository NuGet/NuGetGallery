// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Configuration;

namespace NuGet.Jobs
{
    public class DelegateSqlConnectionFactory<TbDbConfiguration> : ISqlConnectionFactory<TbDbConfiguration>
        where TbDbConfiguration : IDbConfiguration
    {
        private readonly Func<Task<SqlConnection>> _connectionFunc;
        private readonly ILogger<DelegateSqlConnectionFactory<TbDbConfiguration>> _logger;

        public DelegateSqlConnectionFactory(Func<Task<SqlConnection>> connectionFunc, ILogger<DelegateSqlConnectionFactory<TbDbConfiguration>> logger)
        {
            _connectionFunc = connectionFunc ?? throw new ArgumentNullException(nameof(connectionFunc));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<SqlConnection> CreateAsync() => _connectionFunc();

        public async Task<SqlConnection> OpenAsync()
        {
            SqlConnection connection = null;

            try
            {
                _logger.LogDebug("Opening SQL connection...");

                connection = await _connectionFunc();

                await connection.OpenAsync();

                _logger.LogDebug("Opened SQL connection");

                return connection;
            }
            catch (Exception e)
            {
                _logger.LogError(0, e, "Unable to open SQL connection due to exception");

                connection?.Dispose();

                throw;
            }
        }
    }
}
