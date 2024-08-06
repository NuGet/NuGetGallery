// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// An incident that affects a component.
    /// This incident can be used to calculate whether or not there is a downtime on the site.
    /// Is aggregated by <see cref="IncidentGroupEntity"/>.
    /// </summary>
    public class IncidentEntity : AggregatedComponentAffectingEntity<IncidentGroupEntity>
    {
        public const string DefaultPartitionKey = "incidents";

        public IncidentEntity()
        {
        }

        public IncidentEntity(
            string id,
            IncidentGroupEntity group,
            string affectedComponentPath, 
            ComponentStatus affectedComponentStatus, 
            DateTime startTime, 
            DateTime? endTime)
            : base(
                  DefaultPartitionKey, 
                  GetRowKey(id, affectedComponentPath, affectedComponentStatus), 
                  group,
                  affectedComponentPath,
                  startTime,
                  affectedComponentStatus,
                  endTime)
        {
            IncidentApiId = id;
        }

        /// <summary>
        /// The ID in the incident API that refers to this incident.
        /// </summary>
        public string IncidentApiId { get; set; }

        public static string GetRowKey(string id, string affectedComponentPath, ComponentStatus affectedComponentStatus)
        {
            return $"{id}_{Utility.ToRowKeySafeComponentPath(affectedComponentPath)}_{affectedComponentStatus}";
        }
    }
}
