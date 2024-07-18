// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using System;

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// Base implementation of <see cref="IChildEntity{T}"/>.
    /// </summary>
    public class ChildEntity<TParent> : ITableEntity, IChildEntity<TParent>
        where TParent : ITableEntity
    {
        public ChildEntity() { }

        public ChildEntity(
            string partitionKey,
            string rowKey,
            string parentRowKey)
        {
            ParentRowKey = parentRowKey;
        }

        public ChildEntity(
            string partitionKey, 
            string rowKey, 
            TParent entity)
            : this(partitionKey, rowKey, entity.RowKey)
        {
        }

        public string ParentRowKey { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        /// <remarks>
        /// This is a readonly property we would like to serialize.
        /// Unfortunately, it must have a public getter and a public setter for <see cref="TableEntity"/> to serialize it.
        /// The empty setter is intended to trick <see cref="TableEntity"/> into serializing it.
        /// See https://github.com/Azure/azure-storage-net/blob/e01de1b34c316255f1ffe8f5e80917150325b088/Lib/Common/Table/TableEntity.cs#L426
        /// </remarks>
        public bool IsLinked
        {
            get { return !string.IsNullOrEmpty(ParentRowKey); }
            set { }
        }   
    }
}