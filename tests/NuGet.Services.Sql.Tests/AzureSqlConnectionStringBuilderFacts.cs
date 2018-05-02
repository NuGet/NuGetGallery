// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.SqlClient;
using Xunit;

namespace NuGet.Services.Sql.Tests
{
    public class AzureSqlConnectionStringBuilderFacts
    {
        private const string AadTenant = "ffffffff-ffff-ffff-ffff-ffffffffffff";
        private const string AadClientId = "00000000-0000-0000-0000-000000000000";
        private const string AadCertificate = "MyAadCertificate";

        [Fact]
        public void ExtractsAadSettingsFromConnectionString()
        {
            // Arrange
            var sqlConnectionString = "Data Source=tcp:noop.database.windows.net;Initial Catalog=noop;" +
                "Persist Security Info=False;Connect Timeout=30;Encrypt=True;TrustServerCertificate=False";
            var aadConnectionString = $"{sqlConnectionString};AadTenant={AadTenant};AadClientId={AadClientId};AadCertificate={AadCertificate}";

            // Act
            var builder = new AzureSqlConnectionStringBuilder(aadConnectionString);
            var sqlBuilder = new SqlConnectionStringBuilder(sqlConnectionString);

            // Assert
            Assert.Equal(sqlBuilder.ConnectionString, builder.ConnectionString, ignoreCase: true);
            Assert.NotEmpty(builder.AadAuthority);

            Assert.Equal(AadTenant, builder.AadTenant);
            Assert.Equal(AadClientId, builder.AadClientId);
            Assert.Equal(AadCertificate, builder.AadCertificate);
            Assert.True(builder.AadSendX5c);
        }

        [Fact]
        public void PreservesSqlSettings()
        {
            // Arrange
            var sqlConnectionString = "Data Source=tcp:noop.database.windows.net;Initial Catalog=noop;" +
                "Integrated Security=False;User ID=SqlUser;Password=SqlPassword;Connect Timeout=30;Encrypt=True";

            // Act
            var builder = new AzureSqlConnectionStringBuilder(sqlConnectionString);
            var sqlBuilder = new SqlConnectionStringBuilder(sqlConnectionString);

            // Assert
            Assert.Equal(sqlBuilder.ConnectionString, builder.ConnectionString, ignoreCase: true);
            Assert.True(string.IsNullOrEmpty(builder.AadAuthority));

            Assert.Equal(builder["Data Source"], sqlBuilder.DataSource);
            Assert.Equal(builder["Initial Catalog"], sqlBuilder.InitialCatalog);
            Assert.Equal(builder["User ID"], sqlBuilder.UserID);
            Assert.Equal(builder["Password"], sqlBuilder.Password);
        }
    }
}
