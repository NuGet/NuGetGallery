// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Incidents;
using NuGet.Services.Status;
using System;

namespace StatusAggregator.Parse
{
    /// <summary>
    /// Describes how a <see cref="Incident"/> affects a <see cref="ServiceStatus"/>.
    /// </summary>
    public class ParsedIncident
    {
        public ParsedIncident(
            Incident incident, 
            string affectedComponentPath,
            ComponentStatus affectedComponentStatus)
        {
            if (incident == null)
            {
                throw new ArgumentNullException(nameof(incident));
            }

            Id = incident.Id;
            CreationTime = incident.Source.CreateDate;
            MitigationTime = incident.MitigationData?.Date;
            AffectedComponentPath = affectedComponentPath;
            AffectedComponentStatus = affectedComponentStatus;
        }

        public string Id { get; set; }
        public string AffectedComponentPath { get; set; }
        public ComponentStatus AffectedComponentStatus { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? MitigationTime { get; set; }
    }
}
