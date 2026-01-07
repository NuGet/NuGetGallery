// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Validation.PackageSigning.ValidateCertificate
{
    public class CertificateVerificationException : Exception
    {
        /// <summary>
        /// Exception thrown by unexpected failures to validate a certificate.
        /// </summary>
        /// <param name="message">The message describing the failure.</param>
        public CertificateVerificationException(string message)
            : base(message)
        {
        }
    }
}
