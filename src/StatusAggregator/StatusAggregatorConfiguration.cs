// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace StatusAggregator
{
    public class StatusAggregatorConfiguration
    {
        /// <summary>
        /// A connection string for the storage account to use.
        /// </summary>
        public string StorageAccount { get; set; }

        /// <summary>
        /// The container name to export the <see cref="ServiceStatus"/> to.
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// The table name to persist the <see cref="ServiceStatus"/> in.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// A connection string for the secondary storage account to use.
        /// </summary>
        public string StorageAccountSecondary { get; set; }

        /// <summary>
        /// A list of environments to filter incidents by.
        /// See <see cref="EnvironmentFilter"/>.
        /// </summary>
        public IEnumerable<string> Environments { get; set; }

        /// <summary>
        /// The maximum severity of any incidents to process.
        /// See <see cref="SeverityFilter"/>.
        /// </summary>
        public int MaximumSeverity { get; set; } = int.MaxValue;

        /// <summary>
        /// A team ID to use to query the incident API.
        /// </summary>
        public string TeamId { get; set; }

        /// <summary>
        /// The number of minutes that must pass before a message is created for a recently started event.
        /// </summary>
        public int EventStartMessageDelayMinutes { get; set; } = 15;

        /// <summary>
        /// The number of minutes that must pass before an event whose incidents have all been mitigated is deactivated.
        /// In other words, <see cref="IncidentGroupUpdater"/> will wait this amount of time before it deactivates an event with all mitigated incidents.
        /// </summary>
        public int EventEndDelayMinutes { get; set; } = 15;

        /// <summary>
        /// The number of days that a deactivated event is visible in the <see cref="ServiceStatus"/>.
        /// An event is only added to <see cref="ServiceStatus.Events"/> by <see cref="StatusExporter"/> if it is active or it has been deactivated for less than this number of days.
        /// </summary>
        public int EventVisibilityPeriodDays { get; set; } = 10;
    }
}
