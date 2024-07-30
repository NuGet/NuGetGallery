// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Validation;

namespace NuGet.Jobs.Validation.PackageSigning.Messages
{
    /// <summary>
    /// The message used to kick off a Certificate Validation.
    /// </summary>
    public class CertificateValidationMessage
    {
        public CertificateValidationMessage(
            long certificateKey,
            Guid validationId,
            bool revalidateRevokedCertificate,
            bool sendCheckValidator)
        {
            CertificateKey = certificateKey;
            ValidationId = validationId;
            RevalidateRevokedCertificate = revalidateRevokedCertificate;
            SendCheckValidator = sendCheckValidator;
        }

        /// <summary>
        /// The key to the certificate that should be validated.
        /// </summary>
        public long CertificateKey { get; }

        /// <summary>
        /// This validation's identifier. Certificate validations may share the same validation
        /// id (used to validate multiple certificates in one validation).
        /// </summary>
        public Guid ValidationId { get; }

        /// <summary>
        /// Whether a revoked certificate should be revalidated. By default, Certificate Authorities
        /// are not required to keep a certificate's revocation information forever, therefore, revoked
        /// certificates should only be revalidated in special cases such as a manual revalidation gesture
        /// by a NuGet Admin.
        /// </summary>
        public bool RevalidateRevokedCertificate { get; }

        /// <summary>
        /// Whether or not to send a <see cref="PackageValidationMessageData.CheckValidator"/> message at the end of
        /// the validation.
        /// </summary>
        public bool SendCheckValidator { get; }
    }
}
