// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <summary>
    /// Represents ID-level (package registration) sponsorship metadata retrieved by db2catalog.
    /// </summary>
    public sealed class PackageRegistrationSponsorshipDetails
    {
        public PackageRegistrationSponsorshipDetails(
            string packageId,
            DateTime registrationLastEdited,
            List<string> sponsorshipUrls)
        {
            PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
            RegistrationLastEdited = registrationLastEdited;
            SponsorshipUrls = sponsorshipUrls;
        }

        public string PackageId { get; }
        public DateTime RegistrationLastEdited { get; }
        public List<string> SponsorshipUrls { get; }
    }
}
