// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The metadata for a package's signing. A package may be signed with one or more <see cref="PackageSignature"/>s
    /// using one or more <see cref="EndCertificate"/>s.
    /// </summary>
    public class PackageSigningState
    {
        /// <summary>
        /// The key referencing a package in the NuGet Gallery database. If a package is hard deleted then re-pushed,
        /// the <see cref="PackageId"/> and <see cref="PackageNormalizedVersion"/> version will be the same but the
        /// <see cref="PackageKey"/> will be different.
        /// </summary>
        public int PackageKey { get; set; }

        /// <summary>
        /// The package ID. Has a maximum length of 128 unicode characters as defined by the NuGet Gallery database.
        /// </summary>
        public string PackageId { get; set; }

        /// <summary>
        /// The normalized package version. Has a maximum length of 64 unicode characters as defined by the NuGet
        /// Gallery database.
        /// </summary>
        public string PackageNormalizedVersion { get; set; }

        /// <summary>
        /// The status of the package's signing. The SigningStatus will only be "Valid" if and only if all of this
        /// Package's <see cref="PackageSignature"/>s' Status is "Valid" or "InGracePeriod".
        /// </summary>
        public PackageSigningStatus SigningStatus { get; set; }

        /// <summary>
        /// The signatures used to ensure this package's integerity.
        /// </summary>
        public virtual ICollection<PackageSignature> PackageSignatures { get; set; }
    }
}
