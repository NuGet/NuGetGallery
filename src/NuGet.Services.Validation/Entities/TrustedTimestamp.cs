// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// A timestamp created by a <see href="https://tools.ietf.org/html/rfc3161">Trusted Timestamp Authority</see>.
    /// </summary>
    public class TrustedTimestamp
    {
        /// <summary>
        /// The unique identifier for this trusted timestamp.
        /// </summary>
        public long Key { get; set; }

        /// <summary>
        /// The key to the <see cref="PackageSignature"/> that depends on this trusted timestamp.
        /// </summary>
        public long PackageSignatureKey { get; set; }

        /// <summary>
        /// The key to the end <see cref="EndCertificate"/> used to create this trusted timestamp.
        /// </summary>
        public long EndCertificateKey { get; set; }

        /// <summary>
        /// The value contained by this trusted timestamp, in UTC.
        /// </summary>
        public DateTime Value { get; set; }

        /// <summary>
        /// The last known status for this timestamp. This may be stale.
        /// </summary>
        public TrustedTimestampStatus Status { get; set; }

        /// <summary>
        /// The <see cref="PackageSignature"/> that depends on this trusted timestamp. If this
        /// timestamp's <see cref="EndCertificate"/> is revoked, the signatures MUST be invalidated.
        /// </summary>
        public PackageSignature PackageSignature { get; set; }

        /// <summary>
        /// The end <see cref="EndCertificate"/> used to create this trusted timestamp. If this certificate
        /// is revoked, the <see cref="PackageSignature"/> MUST be invalidated. However, if the certificate
        /// expires, the signature SHOULD NOT be invalidated.
        /// </summary>
        public EndCertificate EndCertificate { get; set; }
    }
}
