// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Services.Status.Table.Manual
{
    public class ManualStatusChangeEntity : ITableEntity
    {
        public const string DefaultPartitionKey = "manual";

        private ITableEntity _tableEntity;

        public ManualStatusChangeEntity()
        {
            _tableEntity = new TableEntity();
        }

        protected ManualStatusChangeEntity(
            ManualStatusChangeType type)
        {
            Type = (int)type;
            _tableEntity = new TableEntity(DefaultPartitionKey, GetRowKey(Guid.NewGuid()));
        }

        public Guid Guid => Guid.Parse(RowKey);

        /// <remarks>
        /// This should be a <see cref="ManualStatusChangeType"/> converted to an enum.
        /// See https://github.com/Azure/azure-storage-net/issues/383
        /// </remarks>
        public int Type { get; set; }
        public string PartitionKey { get => _tableEntity.PartitionKey; set => _tableEntity.PartitionKey = value; }
        public string RowKey { get => _tableEntity.RowKey; set => _tableEntity.RowKey = value; }
        public DateTimeOffset? Timestamp { get => _tableEntity.Timestamp; set => _tableEntity.Timestamp = value; }
        public ETag ETag { get => _tableEntity.ETag; set => _tableEntity.ETag = value; }

        public static string GetRowKey(Guid guid)
        {
            return guid.ToString();
        }
    }
}
