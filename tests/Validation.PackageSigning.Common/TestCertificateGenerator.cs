// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public sealed class TestCertificateGenerator
    {
        public DateTimeOffset NotBefore { get; set; }

        public DateTimeOffset NotAfter { get; set; }

        public byte[] SerialNumber { get; private set; }

        public Collection<X509Extension> Extensions { get; }

        public TestCertificateGenerator()
        {
            Extensions = new Collection<X509Extension>();
        }

        public void SetSerialNumber(byte[] serialNumber)
        {
            SerialNumber = serialNumber ?? throw new ArgumentNullException(nameof(serialNumber));
        }
    }
}
