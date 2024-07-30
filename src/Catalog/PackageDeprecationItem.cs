// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.Metadata.Catalog
{
    public class PackageDeprecationItem
    {
        /// <param name="reasons">The list of reasons a package was deprecated.</param>
        /// <param name="message">An additional message associated with a package, if one exists.</param>
        /// <param name="alternatePackageId">
        /// The ID of a package that can be used alternatively. Must be specified if <paramref name="alternatePackageRange"/> is specified.
        /// </param>
        /// <param name="alternatePackageRange">
        /// A string representing the version range of a package that can be used alternatively. Must be specified if <paramref name="alternatePackageId"/> is specified.
        /// </param>
        public PackageDeprecationItem(
            IReadOnlyList<string> reasons,
            string message,
            string alternatePackageId,
            string alternatePackageRange)
        {
            if (reasons == null)
            {
                throw new ArgumentNullException(nameof(reasons));
            }

            if (!reasons.Any())
            {
                throw new ArgumentException(nameof(reasons));
            }

            Reasons = reasons;
            Message = message;
            AlternatePackageId = alternatePackageId;
            AlternatePackageRange = alternatePackageRange;

            if (AlternatePackageId == null && AlternatePackageRange != null)
            {
                throw new ArgumentException(
                    "Cannot specify an alternate package version range if an alternate package ID is not provided.", 
                    nameof(AlternatePackageRange));
            }
            
            if (AlternatePackageId != null && AlternatePackageRange == null)
            {
                throw new ArgumentException(
                    "Cannot specify an alternate package ID if an alternate package version range is not provided.",
                    nameof(AlternatePackageId));
            }
        }

        public IReadOnlyList<string> Reasons { get; }
        public string Message { get; }
        public string AlternatePackageId { get; }
        public string AlternatePackageRange { get; }
    }
}
