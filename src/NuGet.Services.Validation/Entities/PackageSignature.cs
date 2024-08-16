// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// The signature for a <see cref="PackageSigningState"/>.
    /// </summary>
    public class PackageSignature
    {
        /// <summary>
        /// The database-mastered identifier for this signature.
        /// </summary>
        public long Key { get; set; }

        /// <summary>
        /// The key to the <see cref="PackageSigningState"/> this signature is for.
        /// </summary>
        public int PackageKey { get; set; }

        /// <summary>
        /// The key to the end <see cref="Validation.EndCertificate"/> used to create this package signature.
        /// </summary>
        public long EndCertificateKey { get; set; }

        /// <summary>
        /// The time at which this record was created, used to detect signatures that are
        /// stuck <see cref="PackageSignatureStatus.InGracePeriod"/>.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The status for this signature.
        /// </summary>
        public PackageSignatureStatus Status { get; set; }

        /// <summary>
        /// The type of this signature.
        /// </summary>
        public PackageSignatureType Type { get; set; }

        /// <summary>
        /// Used for optimistic concurrency when updating package signatures.
        /// </summary>
        public byte[] RowVersion { get; set; }

        /// <summary>
        /// The <see cref="PackageSigningState"/> that owns this <see cref="PackageSignature"/>. If this signature
        /// has a Status of "Invalid", the overall <see cref="PackageSigningState"/> will also be "Invalid". Note that
        /// a <see cref="PackageSigningState"/> may have multiple <see cref="PacakgeSignature"/>s. Thus, the overall
        /// signing state can be invalid even if this signature is valid.
        /// </summary>
        public virtual PackageSigningState PackageSigningState { get; set; }

        /// <summary>
        /// The <see cref="TrustedTimestamp"/>s that dates when this signature was created.
        /// </summary>
        public virtual ICollection<TrustedTimestamp> TrustedTimestamps { get; set; }

        /// <summary>
        /// The end <see cref="Validation.EndCertificate"/> used to create this package signature.
        /// </summary>
        public virtual EndCertificate EndCertificate { get; set; }
    }
}
