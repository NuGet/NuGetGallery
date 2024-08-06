﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// Base implementation of <see cref="IComponentAffectingEntity"/>.
    /// </summary>
    public class ComponentAffectingEntity : ITableEntity, IComponentAffectingEntity
    {
        public ComponentAffectingEntity()
        {
        }

        public ComponentAffectingEntity(
            string partitionKey,
            string rowKey,
            string affectedComponentPath,
            DateTime startTime,
            ComponentStatus affectedComponentStatus = ComponentStatus.Up,
            DateTime? endTime = null)
        {
            AffectedComponentPath = affectedComponentPath;
            AffectedComponentStatus = (int)affectedComponentStatus;
            StartTime = startTime;
            EndTime = endTime;
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public string AffectedComponentPath { get; set; }

        /// <remarks>
        /// This should be a <see cref="ComponentStatus"/> converted to an enum.
        /// See https://github.com/Azure/azure-storage-net/issues/383
        /// </remarks>
        public int AffectedComponentStatus { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        /// <remarks>
        /// This is a readonly property we would like to serialize.
        /// Unfortunately, it must have a public getter and a public setter for <see cref="TableEntity"/> to serialize it.
        /// The empty setter is intended to trick <see cref="TableEntity"/> into serializing it.
        /// See https://github.com/Azure/azure-storage-net/blob/e01de1b34c316255f1ffe8f5e80917150325b088/Lib/Common/Table/TableEntity.cs#L426
        /// </remarks>
        public bool IsActive
        {
            get { return EndTime == null; }
            set { }
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
