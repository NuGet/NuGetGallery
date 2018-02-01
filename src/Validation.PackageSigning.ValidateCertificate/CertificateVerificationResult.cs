// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
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
        /// The status of the end <see cref="X509Certificate2"/>.
        /// </summary>
        public EndCertificateStatus Status { get; set; }

        /// <summary>
        /// The flattened flags for the <see cref="X509Certificate2"/> and its entire chain.
        /// </summary>
        public X509ChainStatusFlags StatusFlags { get; set; }

        /// <summary>
        /// The time that the end <see cref="X509Certificate2"/>'s status was last updated, according to the
        /// Certificate Authority. If <see cref="Status"/> is <see cref="EndCertificateStatus.Revoked"/>
        /// or <see cref="EndCertificateStatus.Unknown"/>, this will have a value of <c>null</c>.
        /// </summary>
        public DateTime? StatusUpdateTime { get; set; }

        /// <summary>
        /// The time at which the end <see cref="X509Certificate2"/> was revoked. If <see cref="Status"/>
        /// is not <see cref="CertificateStatus.Revoked"/>, this will have a value of <c>null</c>.
        /// </summary>
        public DateTime? RevocationTime { get; set; }
    }
}
