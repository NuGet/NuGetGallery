// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Services.Sql;

namespace Search.GenerateAuxiliaryData
{
    internal sealed class VerifiedPackagesExporter : SqlExporter
    {
        private const string _colPackageId = "Id";
        private readonly string _verifiedPackagesScript;

        public VerifiedPackagesExporter(
            ILogger<SqlExporter> logger,
            ISqlConnectionFactory connectionFactory,
            CloudBlobContainer defaultDestinationContainer,
            string defaultVerifiedPackagesScript,
            string defaultName)
            : base(logger, connectionFactory, defaultDestinationContainer, defaultName)
        {
            _verifiedPackagesScript = defaultVerifiedPackagesScript;
        }

        protected override JContainer GetResultOfQuery(SqlConnection connection)
        {
            var verifiedPackagesCommand = new SqlCommand(GetEmbeddedSqlScript(_verifiedPackagesScript), connection);
            verifiedPackagesCommand.CommandType = CommandType.Text;
            verifiedPackagesCommand.CommandTimeout = 60;

            SqlDataReader reader = null;

            try
            {
                reader = verifiedPackagesCommand.ExecuteReader();

                return GetVerifiedPackages(reader);
            }
            finally
            {
                reader?.Close();
            }
        }

        internal JArray GetVerifiedPackages(IDataReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            var colNames = GetColMappingFromSqlDataReader(reader);
            var result = new JArray();

            while (reader.Read())
            {
                result.Add(reader.GetString(colNames[_colPackageId]));
            }

            return result;
        }
    }
}