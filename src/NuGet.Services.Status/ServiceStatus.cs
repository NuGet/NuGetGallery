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
        /// <remarks>
        /// Every time the status backend runs it will update this field to the current time.
        /// </remarks>
        public DateTime LastBuilt { get; }

        /// <summary>
        /// The last time the status was updated.
        /// </summary>
        /// <remarks>
        /// This field is updated to the current time whenever the status backend is able to successfully refresh its information.
        /// If the status backend is running, but is unable to refresh its information, this field will stagnate.
        /// </remarks>
        public DateTime LastUpdated { get; }

        /// <summary>
        /// The <see cref="IReadOnlyComponent"/> that describes the entire service.
        /// Its <see cref="IReadOnlyComponent.SubComponents"/> represent portions of the service.
        /// </summary>
        public IReadOnlyComponent ServiceRootComponent { get; }

        /// <summary>
        /// A list of recent <see cref="Event"/>s regarding the service.
        /// </summary>
        public IEnumerable<Event> Events { get; }

        public ServiceStatus(DateTime lastUpdated, IReadOnlyComponent serviceRootComponent, IEnumerable<Event> events)
            : this(DateTime.Now, lastUpdated, serviceRootComponent, events)
        {
        }

        public ServiceStatus(DateTime lastUpdated, IComponent serviceRootComponent, IEnumerable<Event> events)
            : this(DateTime.Now, lastUpdated, serviceRootComponent, events)
        {
        }

        public ServiceStatus(DateTime lastBuilt, DateTime lastUpdated, IReadOnlyComponent serviceRootComponent, IEnumerable<Event> events)
        {
            LastBuilt = lastBuilt;
            LastUpdated = lastUpdated;
            ServiceRootComponent = serviceRootComponent;
            Events = events;
        }

        public ServiceStatus(DateTime lastBuilt, DateTime lastUpdated, IComponent serviceRootComponent, IEnumerable<Event> events)
            : this(lastBuilt, lastUpdated, new ReadOnlyComponent(serviceRootComponent), events)
        {
        }

        [JsonConstructor]
        public ServiceStatus(DateTime lastBuilt, DateTime lastUpdated, ReadOnlyComponent serviceRootComponent, IEnumerable<Event> events)
            : this(lastBuilt, lastUpdated, (IReadOnlyComponent)serviceRootComponent, events)
        {
        }
    }
}
