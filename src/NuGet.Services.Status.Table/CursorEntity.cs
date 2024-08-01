// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Services.Status.Table
{
    public class CursorEntity : ITableEntity
    {
        public const string DefaultPartitionKey = "cursors";

        public CursorEntity()
        {
        }

        public CursorEntity(string name, DateTime value)
        {
            Value = value;
            PartitionKey = DefaultPartitionKey;
            RowKey = GetRowKey(name);
        }

        public string Name => RowKey;

        public DateTime Value { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public static string GetRowKey(string name)
        {
            return name;
        }
    }
}
