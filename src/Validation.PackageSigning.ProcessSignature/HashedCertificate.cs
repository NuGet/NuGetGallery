// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    /// <summary>
    /// A certificate and its corresponding SHA-256 thumbprint, represented as a lowercase hexadecimal string. This
    /// type is meant to be a convenient way to avoid rehashing the certificate over and over.
    /// </summary>
    public class HashedCertificate
    {
        public HashedCertificate(X509Certificate2 certificate)
        {
            Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
            Thumbprint = certificate.ComputeSHA256Thumbprint();
        }

        public X509Certificate2 Certificate { get; }
        public string Thumbprint { get; }
    }
}
