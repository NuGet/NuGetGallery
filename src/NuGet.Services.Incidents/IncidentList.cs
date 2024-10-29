// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace NuGet.Services.Incidents
{
    internal class IncidentList
    {
        [JsonProperty(PropertyName = "value")]
        public IEnumerable<Incident> Incidents { get; set; }

        [JsonProperty(PropertyName = "odata.nextLink")]
        public Uri NextLink { get; set; }
    }
}
