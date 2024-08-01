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

        public ManualStatusChangeEntity()
        {
        }

        protected ManualStatusChangeEntity(
            ManualStatusChangeType type)
        {
            Type = (int)type;
            PartitionKey = DefaultPartitionKey;
            RowKey = GetRowKey(Guid.NewGuid());
        }

        public Guid Guid => Guid.Parse(RowKey);

        /// <remarks>
        /// This should be a <see cref="ManualStatusChangeType"/> converted to an enum.
        /// See https://github.com/Azure/azure-storage-net/issues/383
        /// </remarks>
        public int Type { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public static string GetRowKey(Guid guid)
        {
            return guid.ToString();
        }
    }
}
