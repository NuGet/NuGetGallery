// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Services.FeatureFlags;

namespace NuGetGallery.Auditing.AuditedEntities
{
    public class AuditedFeatureFlagFlight
    {
        public string Name { get; private set; }
        public bool All { get; private set; }
        public bool SiteAdmins { get; private set; }
        public string[] Accounts { get; private set; }
        public string[] Domains { get; private set; }

        public static AuditedFeatureFlagFlight[] CreateFrom(FeatureFlags flags)
        {
            return flags.Flights?
                .Select(f => CreateFrom(f.Key, f.Value))
                .ToArray() ?? new AuditedFeatureFlagFlight[0];
        }

        public static AuditedFeatureFlagFlight CreateFrom(string name, Flight flight)
        {
            return new AuditedFeatureFlagFlight
            {
                Name = name,
                All = flight.All,
                SiteAdmins = flight.SiteAdmins,
                Accounts = flight.Accounts?.ToArray() ?? new string[0],
                Domains = flight.Domains?.ToArray() ?? new string[0]
            };
        }
    }
}
