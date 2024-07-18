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

        private ITableEntity _tableEntity;

        public CursorEntity()
        {
            _tableEntity = new TableEntity();
        }

        public CursorEntity(string name, DateTime value)
        {
            Value = value;
            _tableEntity = new TableEntity(DefaultPartitionKey, GetRowKey(name));
        }

        public string Name => RowKey;

        public DateTime Value { get; set; }
        public string PartitionKey { get => _tableEntity.PartitionKey; set => _tableEntity.PartitionKey = value; }
        public string RowKey { get => _tableEntity.RowKey; set => _tableEntity.RowKey = value; }
        public DateTimeOffset? Timestamp { get => _tableEntity.Timestamp; set => _tableEntity.Timestamp = value; }
        public ETag ETag { get => _tableEntity.ETag; set => _tableEntity.ETag = value; }

        public static string GetRowKey(string name)
        {
            return name;
        }
    }
}
