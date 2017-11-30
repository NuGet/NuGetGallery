// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// A X.509 Intermediary or Root Certificate used by one or more <see cref="EndCertificate" />s, 
    /// linked together by a <see cref="CertificateChainLink"/>, used by one or more <see cref="PackageSignature"/>s.
    /// </summary>
    public class ParentCertificate
    {
        /// <summary>
        /// The database-mastered identifier for this certificate.
        /// </summary>
        public long Key { get; set; }

        /// <summary>
        /// The SHA-256 thumbprint (fingerprint) that uniquely identifies this certificate. This is a string with
        /// exactly 64 characters and is the hexadecimal encoding of the hash digest. Note that the SQL column that
        /// stores this property allows longer string values to facilitate future hash algorithm changes.
        /// </summary>
        public string Thumbprint { get; set; }

        /// <summary>
        /// The <see cref="CertificateChainLink"/>s this parent-certificate is part of.
        /// </summary>
        public virtual ICollection<CertificateChainLink> CertificateChainLinks { get; set; }
    }
}
