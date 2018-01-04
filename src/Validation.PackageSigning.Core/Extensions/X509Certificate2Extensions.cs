// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace System.Security.Cryptography.X509Certificates
{
    public static class X509Certificate2Extensions
    {
        public static string ComputeSHA256Thumbprint(this X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }
            
            using (var sha256 = SHA256.Create())
            {
                var digestBytes = sha256.ComputeHash(certificate.RawData);
                return BitConverter
                        .ToString(digestBytes)
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
            }
        }
    }
}
