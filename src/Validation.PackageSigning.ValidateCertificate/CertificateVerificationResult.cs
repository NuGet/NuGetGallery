// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.ValidateCertificate
{
    /// <summary>
    /// The result of a <see cref="X509Certificate2"/> verification by the
    /// <see cref="ICertificateValidationService"/>.
    /// </summary>
    public class CertificateVerificationResult
    {
        /// <summary>
        /// Create a new non-revoked certificate verification result.
        /// </summary>
        /// <param name="status">The status of the <see cref="X509Certificate2"/></param>
        /// <param name="revocationTime">The time of </param>
        public CertificateVerificationResult(EndCertificateStatus status)
        {
            if (status == EndCertificateStatus.Revoked)
            {
                throw new ArgumentException("Provide a revocation date for a revoked certificate result.", nameof(status));
            }

            Status = status;
        }

        /// <summary>
        /// Create a new revoked certificate verification result.
        /// </summary>
        /// <param name="revocationTime">The start of the certificate's invalidity period.</param>
        public CertificateVerificationResult(DateTime revocationTime)
        {
            Status = EndCertificateStatus.Revoked;
            RevocationTime = revocationTime;
        }

        /// <summary>
        /// The status of the <see cref="X509Certificate2"/>.
        /// </summary>
        public EndCertificateStatus Status { get; }

        /// <summary>
        /// The time at which the <see cref="X509Certificate2"/> was revoked. Null unless
        /// <see cref="Status"/> is <see cref="CertificateStatus.Revoked"/>.
        /// </summary>
        public DateTime? RevocationTime { get; }
    }
}
