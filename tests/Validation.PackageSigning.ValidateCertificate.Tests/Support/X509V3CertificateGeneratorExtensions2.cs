// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Org.BouncyCastle.Asn1.X509;

namespace Org.BouncyCastle.X509
{
    public static class X509V3CertificateGeneratorExtensions2
    {
        public static void AddTimestampingEku(this X509V3CertificateGenerator generator)
        {
            // TimeStamping EKU
            var usages = new[] { KeyPurposeID.IdKPTimeStamping };

            generator.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id,
                critical: true,
                extensionValue: new ExtendedKeyUsage(usages));
        }
    }
}