// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Jobs.Catalog2Registration
{
    /// <summary>
    /// Applies ID-level (package registration scoped) attributes to the registration index blobs. This is the consumer
    /// side of the version-less catalog leaves emitted by the db2catalog registration lane. Unlike
    /// <see cref="IRegistrationUpdater"/>, which operates on a specific package version, this updater does a
    /// read-modify-write of the registration index itself, touching only the ID-level fields (the first of which is the
    /// set of sponsorship URLs).
    /// </summary>
    public interface IRegistrationLevelUpdater
    {
        /// <summary>
        /// Sets the ID-level sponsorship URLs on the registration index across all hives. An empty or null
        /// <paramref name="sponsorshipUrls"/> removes the field entirely, which signals that sponsorship has been
        /// fully retracted for the package ID.
        /// </summary>
        /// <param name="id">The package ID whose registration index should be updated.</param>
        /// <param name="sponsorshipUrls">The sponsorship URLs to set, or null/empty to remove them.</param>
        /// <param name="commitTimestamp">The catalog commit timestamp that produced this ID-level change.</param>
        Task UpdateAsync(string id, IReadOnlyList<string> sponsorshipUrls, DateTimeOffset commitTimestamp);
    }
}
