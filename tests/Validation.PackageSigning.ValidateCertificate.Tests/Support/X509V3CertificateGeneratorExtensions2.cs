// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NuGet.Packaging.Signing;

namespace Validation.PackageSigning.ValidateCertificate.Tests.Support
{
    public static class X509V3CertificateGeneratorExtensions2
    {
        public static void AddTimestampingEku(this CertificateRequest certificateRequest)
        {
            var usages = new OidCollection { new Oid(Oids.TimeStampingEku) };

            certificateRequest.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    usages,
                    critical: true));
        }
    }
}
