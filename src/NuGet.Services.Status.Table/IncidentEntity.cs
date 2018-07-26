// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// Class used to serialize an incident in a table.
    /// </summary>
    public class IncidentEntity : TableEntity
    {
        public const string DefaultPartitionKey = "incidents";

        public IncidentEntity()
        {
        }

        public IncidentEntity(
            string id, 
            string affectedComponentPath, 
            ComponentStatus affectedComponentStatus, 
            DateTime creationTime, 
            DateTime? mitigationTime)
            : base(DefaultPartitionKey, GetRowKey(id, affectedComponentPath, affectedComponentStatus))
        {
            IncidentApiId = id;
            AffectedComponentPath = affectedComponentPath;
            AffectedComponentStatus = (int)affectedComponentStatus;
            CreationTime = creationTime;
            MitigationTime = mitigationTime;
        }

        public string EventRowKey { get; set; }

        /// <remarks>
        /// This is a readonly property we would like to serialize.
        /// Unfortunately, it must have a public getter and a public setter for <see cref="TableEntity"/> to serialize it.
        /// The empty setter is intended to trick <see cref="TableEntity"/> into serializing it.
        /// See https://github.com/Azure/azure-storage-net/blob/e01de1b34c316255f1ffe8f5e80917150325b088/Lib/Common/Table/TableEntity.cs#L426
        /// </remarks>
        public bool IsLinkedToEvent
        {
            get { return !string.IsNullOrEmpty(EventRowKey); }
            set { }
        }

        public string IncidentApiId { get; set; }

        public string AffectedComponentPath { get; set; }

        /// <remarks>
        /// This should be a <see cref="ComponentStatus"/> converted to an enum.
        /// See https://github.com/Azure/azure-storage-net/issues/383
        /// </remarks>
        public int AffectedComponentStatus { get; set; }

        public DateTime CreationTime { get; set; }

        public DateTime? MitigationTime { get; set; }

        /// <remarks>
        /// This is a readonly property we would like to serialize.
        /// Unfortunately, it must have a public getter and a public setter for <see cref="TableEntity"/> to serialize it.
        /// The empty setter is intended to trick <see cref="TableEntity"/> into serializing it.
        /// See https://github.com/Azure/azure-storage-net/blob/e01de1b34c316255f1ffe8f5e80917150325b088/Lib/Common/Table/TableEntity.cs#L426
        /// </remarks>
        public bool IsActive
        {
            get { return MitigationTime == null; }
            set { }
        }

        private static string GetRowKey(string id, string affectedComponentPath, ComponentStatus affectedComponentStatus)
        {
            return $"{id}_{Utility.ToRowKeySafeComponentPath(affectedComponentPath)}_{affectedComponentStatus}";
        }
    }
}
