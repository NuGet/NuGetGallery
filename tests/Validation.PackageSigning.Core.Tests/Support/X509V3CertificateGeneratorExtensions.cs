// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
            // TODO: Migrate to "TestCertificateGenerator".
            // See: https://github.com/NuGet/NuGetGallery/issues/8216

            // This was copied from Test.Utility's SigningTestUtility.CertificateModificationGeneratorExpiredCert,
            // which was changed to accept a "TestCertificateGenerator" instead.
            // See: https://github.com/NuGet/NuGet.Client/pull/2685/files#diff-6c1acc7ed04355ba9e02b589e7e32a41L69
            var usages = new[] { KeyPurposeID.IdKPCodeSigning };

            generator.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));

            generator.SetNotBefore(DateTime.UtcNow.AddHours(-1));
            generator.SetNotAfter(DateTime.UtcNow.AddHours(-1));
        }

        public static void AddSigningEku(this X509V3CertificateGenerator generator)
        {
            // TODO: Migrate to "TestCertificateGenerator".
            // See: https://github.com/NuGet/NuGetGallery/issues/8216

            // This was copied from Test.Utility's SigningTestUtility.CertificateModificationGeneratorForCodeSigningEkuCert.
            // which was changed to accept a "TestCertificateGenerator" instead.
            // See: https://github.com/NuGet/NuGet.Client/pull/2685/files#diff-6c1acc7ed04355ba9e02b589e7e32a41L69
            var usages = new[] { KeyPurposeID.IdKPCodeSigning };

            generator.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));
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