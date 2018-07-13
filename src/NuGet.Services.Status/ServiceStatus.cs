// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NuGet.Services.Status
{
    /// <summary>
    /// Describes the status of an entire service.
    /// </summary>
    public class ServiceStatus
    {
        /// <summary>
        /// The time this status was generated.
        /// </summary>
        public DateTime LastUpdated { get; }

        /// <summary>
        /// The <see cref="IReadOnlyComponent"/> that describes the entire service.
        /// Its <see cref="IReadOnlyComponent.SubComponents"/> represent portions of the service.
        /// </summary>
        public IReadOnlyComponent ServiceRootComponent { get; }

        /// <summary>
        /// A list of <see cref="IEvent"/>s that have affected the service recently.
        /// </summary>
        public IEnumerable<IEvent> Events { get; }

        public ServiceStatus(IReadOnlyComponent serviceRootComponent, IEnumerable<IEvent> events)
            : this(DateTime.Now, serviceRootComponent, events)
        {
        }

        public ServiceStatus(DateTime lastUpdated, IReadOnlyComponent serviceRootComponent, IEnumerable<IEvent> events)
        {
            LastUpdated = lastUpdated;
            ServiceRootComponent = serviceRootComponent;
            Events = events;
        }

        [JsonConstructor]
        public ServiceStatus(DateTime lastUpdated, ReadOnlyComponent serviceRootComponent, IEnumerable<Event> events)
            : this(lastUpdated, (IReadOnlyComponent)serviceRootComponent, events)
        {
        }
    }
}
