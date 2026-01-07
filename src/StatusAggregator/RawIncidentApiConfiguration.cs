// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace StatusAggregator
{
    /// <summary>
    /// A configuration model mimicking <see cref="NuGet.Services.Incidents.IncidentApiConfiguration"/> but is more
    /// compatible with the simple deserialization supported by Microsoft.Extensions.Configuration.
    /// </summary>
    public class RawIncidentApiConfiguration
    {
        public string BaseUri { get; set; }
        public string Certificate { get; set; }
    }
}
