// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Test.Utility.Signing;

namespace Org.BouncyCastle.X509
{
    public static class X509V3CertificateGeneratorExtensions
    {
        public static void MakeExpired(this X509V3CertificateGenerator generator)
        {
            SigningTestUtility.CertificateModificationGeneratorExpiredCert(generator);
        }

        public static void AddSigningEku(this X509V3CertificateGenerator generator)
        {
            SigningTestUtility.CertificateModificationGeneratorForCodeSigningEkuCert(generator);
        }

        public static void AddAuthorityInfoAccess(
            this X509V3CertificateGenerator generator,
            CertificateAuthority ca,
            bool addOcsp = false,
            bool addCAIssuers = false)
        {
            var vector = new List<Asn1Encodable>();

            if (addOcsp)
            {
                vector.Add(
                    new AccessDescription(
                        AccessDescription.IdADOcsp,
                        new GeneralName(GeneralName.UniformResourceIdentifier, ca.OcspResponderUri.OriginalString)));
            }

            if (addCAIssuers)
            {
                vector.Add(
                    new AccessDescription(
                        AccessDescription.IdADCAIssuers,
                        new GeneralName(GeneralName.UniformResourceIdentifier, ca.CertificateUri.OriginalString)));
            }

            generator.AddExtension(
                X509Extensions.AuthorityInfoAccess,
                critical: false,
                extensionValue: new DerSequence(vector.ToArray()));
        }
    }
}