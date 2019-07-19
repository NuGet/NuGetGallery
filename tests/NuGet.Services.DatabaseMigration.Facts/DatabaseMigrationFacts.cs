// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.Entity.Migrations;
using System.Reflection;
using Xunit;

namespace NuGet.Services.DatabaseMigration.Facts
{
    public class DatabaseMigrationFacts
    {
        [Fact]
        public void AssertDbMigratorPrivateFieldNotNull()
        {
            var historyRepository = typeof(DbMigrator).
                GetField("_historyRepository",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(historyRepository);

            var connectionField = historyRepository.FieldType.BaseType.GetField(
                "_existingConnection",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(connectionField);
            Assert.True(connectionField.FieldType.IsAssignableFrom(typeof(System.Data.SqlClient.SqlConnection)));
        }
    }
}