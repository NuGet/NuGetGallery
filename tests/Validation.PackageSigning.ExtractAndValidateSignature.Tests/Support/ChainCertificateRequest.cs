// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    /// <summary>
    /// Source:
    /// https://github.com/NuGet/NuGet.Client/blob/8aabf8dcf1d1ffdd6cdc32670eb0a33320adcdf2/test/TestUtilities/Test.Utility/Signing/ChainCertificateRequest.cs
    /// </summary>
    public class ChainCertificateRequest
    {
        public string CrlServerBaseUri { get; set; }

        public string CrlLocalBaseUri { get; set; }

        public bool IsCA { get; set; }

        public bool ConfigureCrl { get; set; } = true;

        public X509Certificate2 Issuer { get; set; }

        public string IssuerDN => Issuer?.Subject;
    }
}
