// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.SqlTypes;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    public sealed class Db2CatalogCursor
    {
        private Db2CatalogCursor(string columnName, DateTime cursorValue, int top)
        {
            ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
            CursorValue = cursorValue < SqlDateTime.MinValue.Value ? SqlDateTime.MinValue.Value : cursorValue;

            if (top <= 0)
            {
                throw new ArgumentOutOfRangeException("Argument value must be a positive non-zero integer.", nameof(top));
            }

            Top = top;
        }

        public string ColumnName { get; }
        public DateTime CursorValue { get; }
        public int Top { get; }

        public static Db2CatalogCursor ByCreated(DateTime since, int top) => new Db2CatalogCursor(Db2CatalogProjectionColumnNames.Created, since, top);
        public static Db2CatalogCursor ByLastEdited(DateTime since, int top) => new Db2CatalogCursor(Db2CatalogProjectionColumnNames.LastEdited, since, top);
    }
}