// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Internal.NuGet.Testing.SignedPackages;
using NuGet.Packaging.Signing;

namespace Validation.PackageSigning.Core
{
    public static class X509V3CertificateGeneratorExtensions
    {
        public static void AddSigningEku(this CertificateRequest certificateRequest)
        {
            var usages = new OidCollection { new Oid(Oids.CodeSigningEku) };

            certificateRequest.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    usages,
                    critical: true));
        }

        public static void AddAuthorityInfoAccess(
            this CertificateRequest certificateRequest,
            CertificateAuthority certificateAuthority,
            bool addOcsp = false,
            bool addCAIssuers = false)
        {
            certificateRequest.CertificateExtensions.Add(
                new X509AuthorityInformationAccessExtension(
                    addOcsp ? certificateAuthority.OcspResponderUri : null,
                    addCAIssuers ? certificateAuthority.CertificateUri : null));
        }
    }
}
