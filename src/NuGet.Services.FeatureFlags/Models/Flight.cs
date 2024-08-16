// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.FeatureFlags
{
    /// <summary>
    /// The state of a specific flight. A flight can enable features for specific users. For example,
    /// the "index package with a license expression" feature could be enabled for only administrators.
    /// Note that all of 
    /// </summary>
    public class Flight
    {
        public Flight(bool all, bool siteAdmins, IReadOnlyList<string> accounts, IReadOnlyList<string> domains)
        {
            All = all;
            SiteAdmins = siteAdmins;
            Accounts = accounts ?? new List<string>();
            Domains = domains ?? new List<string>();
        }

        /// <summary>
        /// Whether this flight is enabled for all users. If true, all other properties are ignored.
        /// </summary>
        public bool All { get; }

        /// <summary>
        /// Whether this flight is enabled for NuGet.org administrators. If false, an administrator
        /// can still be included in the flight by explicitly adding the administrator's account
        /// or email domain to the flight.
        /// </summary>
        public bool SiteAdmins { get; }

        /// <summary>
        /// Specific account usernames that have this flight enabled. This is case insensitive.
        /// </summary>
        public IReadOnlyList<string> Accounts { get; }

        /// <summary>
        /// Specific email domains that have this flight enabled. Example: "microsoft.com" would
        /// enable the flight for "billy@microsoft.com" but not for "bob@nuget.org". This is case insensitive.
        /// </summary>
        public IReadOnlyList<string> Domains { get; }
    }
}
