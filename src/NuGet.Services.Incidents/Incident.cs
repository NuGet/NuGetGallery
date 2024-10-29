// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Incidents
{
    public class Incident
    {
        public string Id { get; set; }
        public int Severity { get; set; }
        public IncidentStatus Status { get; set; }
        public DateTime CreateDate { get; set; }
        public string Title { get; set; }
        public string OwningTeamId { get; set; }
        public IncidentSourceData Source { get; set; }
        public IncidentStateChangeEventData MitigationData { get; set; }
        public IncidentStateChangeEventData ResolutionData { get; set; }
    }

    public class IncidentSourceData
    {
        public DateTime CreateDate { get; set; }
    }

    public class IncidentStateChangeEventData
    {
        public DateTime Date { get; set; }
    }
}
