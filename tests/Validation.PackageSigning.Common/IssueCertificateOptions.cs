// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public sealed class IssueCertificateOptions
    {
        public Action<CertificateRequest> CustomizeCertificate { get; set; }
        public DateTimeOffset NotAfter { get; set; }
        public DateTimeOffset NotBefore { get; set; }

        /// <summary>
        /// Gets or sets the private key for signing the new certificate.
        /// </summary>
        /// <remarks>
        /// Typically:
        ///
        ///     *  If the issue certificate request is for a self-signed root certificate, <see cref="IssuerPrivateKey" />
        ///        should be the private key of <see cref="KeyPair" />.
        ///     *  If the issue certificate request is for any other (non-root) certificate, <see cref="IssuerPrivateKey" />
        ///        should be null, indicating that the private key for the issuing certificate authority should be used.
        /// </remarks>
        public RSA IssuerPrivateKey { get; set; }

        public RSA KeyPair { get; set; }

        public X500DistinguishedName SubjectName { get; set; }

        public Oid SignatureAlgorithm { get; set; }

        public IssueCertificateOptions()
        {
            NotBefore = DateTimeOffset.UtcNow;
            NotAfter = NotBefore.AddHours(2);
            SignatureAlgorithm = new Oid(Oids.Sha256WithRSAEncryption);
        }

        public static IssueCertificateOptions CreateDefaultForRootCertificateAuthority()
        {
            var keyPair = CertificateUtilities.CreateKeyPair();
            var id = CertificateUtilities.GenerateRandomId();
            var subjectName = new X500DistinguishedName($"CN=NuGet Test Root Certificate Authority ({id}),O=NuGet,L=Redmond,S=WA,C=US");

            return new IssueCertificateOptions()
            {
                KeyPair = keyPair,
                IssuerPrivateKey = keyPair,
                SubjectName = subjectName
            };
        }

        public static IssueCertificateOptions CreateDefaultForIntermediateCertificateAuthority()
        {
            var keyPair = CertificateUtilities.CreateKeyPair();
            var id = CertificateUtilities.GenerateRandomId();
            var subjectName = new X500DistinguishedName($"CN=NuGet Test Intermediate Certificate Authority ({id}),O=NuGet,L=Redmond,S=WA,C=US");

            return new IssueCertificateOptions()
            {
                KeyPair = keyPair,
                SubjectName = subjectName
            };
        }

        public static IssueCertificateOptions CreateDefaultForEndCertificate()
        {
            var keyPair = CertificateUtilities.CreateKeyPair();
            var id = CertificateUtilities.GenerateRandomId();
            var subjectName = new X500DistinguishedName($"CN=NuGet Test Certificate ({id}),O=NuGet,L=Redmond,S=WA,C=US");

            return new IssueCertificateOptions()
            {
                KeyPair = keyPair,
                SubjectName = subjectName
            };
        }

        public static IssueCertificateOptions CreateDefaultForTimestampService()
        {
            var keyPair = CertificateUtilities.CreateKeyPair();
            var id = Guid.NewGuid().ToString();
            var subjectName = new X500DistinguishedName($"CN=NuGet Test Timestamp Service ({id}),O=NuGet,L=Redmond,S=WA,C=US");

            return new IssueCertificateOptions()
            {
                KeyPair = keyPair,
                SubjectName = subjectName
            };
        }
    }
}
