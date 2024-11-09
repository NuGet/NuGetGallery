// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    internal static class X509CertificateExtensions
    {
        internal static ReadOnlySpan<byte> GetSerialNumberBigEndian(this X509Certificate certificate)
        {
            if (certificate is null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            byte[] serialNumber = certificate.GetSerialNumber();

            Array.Reverse(serialNumber);

            return serialNumber;
        }
    }
}
