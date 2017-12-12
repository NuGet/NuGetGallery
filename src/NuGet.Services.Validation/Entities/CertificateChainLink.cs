// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Validation
{
    /// <summary>
    /// A link in the certificate chain, describing the relation between a X.509 <see cref="Validation.EndCertificate" />s
    /// and a <see cref="Validation.ParentCertificate"/>.
    /// All links of a given end-certificate to its parent-certificates combined form the certificate chain.
    /// </summary>
    public class CertificateChainLink
    {
        /// <summary>
        /// The database-mastered identifier for this certificate chain.
        /// </summary>
        public long Key { get; set; }

        /// <summary>
        /// The key to the <see cref="Validation.EndCertificate"/> of this certificate chain.
        /// </summary>
        public long EndCertificateKey { get; set; }

        /// <summary>
        /// The key to a <see cref="Validation.ParentCertificate"/> in this certificate chain.
        /// </summary>
        public long ParentCertificateKey { get; set; }

        /// <summary>
        /// The <see cref="Validation.EndCertificate"/> of the certificate chain.
        /// </summary>
        public virtual EndCertificate EndCertificate { get; set; }

        /// <summary>
        /// A <see cref="Validation.ParentCertificate"/> in this certificate chain.
        /// </summary>
        public virtual ParentCertificate ParentCertificate { get; set; }
    }
}