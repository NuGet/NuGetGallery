// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using Test.Utility.Signing;
using BCCertificate = Org.BouncyCastle.X509.X509Certificate;

namespace Validation.PackageSigning.Core.Tests.Support
{
    public static class ExtensionMethods
    {
        public static X509Certificate2 ToX509Certificate2(this BCCertificate certificate)
        {
            return new X509Certificate2(certificate.GetEncoded());
        }

        public static DisposableList<IDisposable> RegisterResponders(
            this ISigningTestServer testServer,
            CertificateAuthority ca,
            bool addCa = true,
            bool addOcsp = true)
        {
            var responders = new DisposableList<IDisposable>();
            var currentCa = ca;

            while (currentCa != null)
            {
                if (addCa)
                {
                    responders.Add(testServer.RegisterResponder(currentCa));
                }

                if (addOcsp)
                {
                    responders.Add(testServer.RegisterResponder(currentCa.OcspResponder));
                }

                currentCa = currentCa.Parent;
            }

            return responders;
        }

        public static DisposableList<IDisposable> RegisterResponders(
            this ISigningTestServer testServer,
            TimestampService timestampService,
            bool addCa = true,
            bool addOcsp = true,
            bool addTimestamper = true)
        {
            var responders = testServer.RegisterResponders(
                timestampService.CertificateAuthority,
                addCa,
                addOcsp);

            if (addTimestamper)
            {
                responders.Add(testServer.RegisterResponder(timestampService));
            }

            return responders;
        }
    }
}
