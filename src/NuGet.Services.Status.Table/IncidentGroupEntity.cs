// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// An aggregation of all of the <see cref="IncidentEntity"/>s affecting a single component during a time period.
    /// Is aggregated by <see cref="EventEntity"/>.
    /// </summary>
    public class IncidentGroupEntity : AggregatedComponentAffectingEntity<EventEntity>
    {
        public const string DefaultPartitionKey = "groups";

        public IncidentGroupEntity()
        {
        }

        public IncidentGroupEntity(
            EventEntity eventEntity,
            string affectedComponentPath,
            ComponentStatus affectedComponentStatus,
            DateTime startTime,
            DateTime? endTime = null)
            : base(
                  DefaultPartitionKey, 
                  GetRowKey(affectedComponentPath, startTime),
                  eventEntity,
                  affectedComponentPath,
                  startTime,
                  affectedComponentStatus,
                  endTime)
        {
        }

        public static string GetRowKey(string affectedComponentPath, DateTime startTime)
        {
            return $"{Utility.ToRowKeySafeComponentPath(affectedComponentPath)}_{startTime.ToString("o")}";
        }
    }
}
