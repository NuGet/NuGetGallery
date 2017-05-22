// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Contains context for <see cref="IValidator"/> when a test is run.
    /// </summary>
    public class ValidationContext
    {
        /// <summary>
        /// The <see cref="PackageIdentity"/> to run the test on.
        /// </summary>
        public PackageIdentity Package { get; set; }

        /// <summary>
        /// The <see cref="CatalogIndexEntry"/>s for the package that were collected.
        /// </summary>
        public IEnumerable<CatalogIndexEntry> Entries { get; set; }

        /// <summary>
        /// The <see cref="AuditRecordHelpers.DeletionAuditEntry"/>s, if any are associated with the <see cref="PackageIdentity"/>.
        /// </summary>
        public IEnumerable<DeletionAuditEntry> DeletionAuditEntries { get; set; }

        /// <summary>
        /// The <see cref="CollectorHttpClient"/> to use when needed.
        /// </summary>
        public CollectorHttpClient Client { get; set; }

        /// <summary>
        /// A <see cref="CancellationToken"/> associated with this run of the test.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }
    }
}
