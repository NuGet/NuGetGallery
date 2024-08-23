﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using Xunit;

namespace CatalogTests.Helpers
{
    public class Db2CatalogCursorTests
    {
        [Theory]
        [MemberData(nameof(CursorMethodToColumnNameMappings))]
        public void TargetsCreatedColumn(Func<DateTime, int, Db2CatalogCursor> cursorMethod, string expectedColumnName)
        {
            const int top = 1;
            var since = DateTime.UtcNow;
            var actual = cursorMethod(since, top);

            Assert.Equal(expectedColumnName, actual.ColumnName);
            Assert.Equal(since, actual.CursorValue);
            Assert.Equal(top, actual.Top);
        }

        [Theory]
        [MemberData(nameof(CursorMethods))]
        public void ProtectsAgainstSqlMinDate(Func<DateTime, int, Db2CatalogCursor> cursorMethod)
        {
            const int top = 1;
            var since = new DateTime(SqlDateTime.MinValue.Value.Ticks - 1, DateTimeKind.Utc);
            var actual = cursorMethod(since, top);

            Assert.Equal(SqlDateTime.MinValue.Value, actual.CursorValue);
        }

        public static IEnumerable<object[]> CursorMethodToColumnNameMappings => new[]
        {
            new object[] { (Func<DateTime, int, Db2CatalogCursor>)((since, top) => Db2CatalogCursor.ByCreated(since, top)), Db2CatalogProjectionColumnNames.Created },
            new object[] { (Func<DateTime, int, Db2CatalogCursor>)((since, top) => Db2CatalogCursor.ByLastEdited(since, top)), Db2CatalogProjectionColumnNames.LastEdited }
        };

        public static IEnumerable<object[]> CursorMethods =>
            from testData in CursorMethodToColumnNameMappings
            select new object[] { testData[0] };
    }
}