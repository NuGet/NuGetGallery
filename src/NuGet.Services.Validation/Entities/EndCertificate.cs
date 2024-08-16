// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    /// <summary>
    /// A X.509 end-certificate, either a signing certificate or a timestamp certificate, 
    /// used by one or more <see cref="PackageSignature" />s for one or more <see cref="PackageSigningState"/>s.
    /// </summary>
    public class EndCertificate
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
        public string Thumbprint { get; set;}

        /// <summary>
        /// The last known status for this certificate. This may be stale.
        /// </summary>
        public EndCertificateStatus Status { get; set; }

        /// <summary>
        /// The use of this end certificate. Today, this is a convenience property implied by
        /// the presence of <see cref="PackageSignatures"/> or <see cref="TrustedTimestamps"/>.
        /// </summary>
        public EndCertificateUse Use { get; set; }

        /// <summary>
        /// The time at which the status was known to be correct, according to the Certificate Authority.
        /// NULL if online verification have never been completed for this certificate.
        /// </summary>
        public DateTime? StatusUpdateTime { get; set; }

        /// <summary>
        /// The time at or before which newer information will be available about the certificate's status,
        /// according to the Certificate Authority. NULL if online verification have never been completed for
        /// this certificate.
        /// </summary>
        public DateTime? NextStatusUpdateTime { get; set; }

        /// <summary>
        /// The last time this certificate's metadata was updated using online verification. NULL if online
        /// verification has never been completed for this certificate.
        /// </summary>
        public DateTime? LastVerificationTime { get; set; }

        /// <summary>
        /// The time at which the certificate was revoked. NULL if the certificate has not been revoked.
        /// </summary>
        public DateTime? RevocationTime { get; set; }

        /// <summary>
        /// The number of times validations for this certificate failed to complete.
        /// This counter should be reset each time the certificate is properly verified (even if the
        /// certificate is found to be in a "Revoked" status).
        /// </summary>
        public int ValidationFailures { get; set; }

        /// <summary>
        /// Used for optimistic concurrency when updating certificates.
        /// </summary>
        public byte[] RowVersion { get; set; }

        /// <summary>
        /// The package signatures that depend on this certificate. If this certificate ever gets revoked,
        /// each of these signatures that were signed after the invalidity period begins MUST be invalidated.
        /// </summary>
        public virtual ICollection<PackageSignature> PackageSignatures { get; set; }

        /// <summary>
        /// The timestamps signed by Trusted Timestamp Authorities that depend on this certificate. If this
        /// certificate is revoked, ALL trusted timestamps and their respective signatures MUST be invalidated.
        /// </summary>
        public virtual ICollection<TrustedTimestamp> TrustedTimestamps { get; set; }

        /// <summary>
        /// A certificate should be periodically validated to ensure it has not be revoked. This is the list
        /// of all validations performed for this certificate.
        /// </summary>
        public virtual ICollection<EndCertificateValidation> Validations { get; set; }

        /// <summary>
        /// An end-certificate is linked to its <see cref="ParentCertificate"/>s (Intermediary and/or Root certificates) by <see cref="CertificateChainLink"/>s.
        /// Combined, this allows for revalidation of the certificate chain.
        /// </summary>
        public virtual ICollection<CertificateChainLink> CertificateChainLinks { get; set; }
    }
}
