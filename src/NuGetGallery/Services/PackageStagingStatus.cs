// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    public class PackageStagingStatus
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the effective expiration deadline in UTC ISO 8601 format.
        /// </summary>
        public string Expires { get; set; }

        public bool Listed { get; set; }
    }
}
