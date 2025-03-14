// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public static class CertificateUtilities
    {
        internal static RSA CreateKeyPair(int strength = 2048)
        {
            return RSA.Create(strength);
        }

        internal static string GenerateFingerprint(X509Certificate2 certificate)
        {
#if NETFRAMEWORK
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(certificate.RawData);

                return BitConverter.ToString(hash).Replace("-", "");
            }
#else
            return certificate.GetCertHashString(HashAlgorithmName.SHA256);
#endif
        }

        internal static string GenerateRandomId()
        {
            return Guid.NewGuid().ToString();
        }

        public static X509Certificate2 GetCertificateWithPrivateKey(X509Certificate2 certificate, RSA keyPair)
        {
            X509Certificate2 certificateWithPrivateKey = certificate.CopyWithPrivateKey(keyPair);
#if NET
            return certificateWithPrivateKey;
#else
            using (certificateWithPrivateKey)
            {
                return new X509Certificate2(certificateWithPrivateKey.Export(X509ContentType.Pfx));
            }
#endif
        }

        internal static byte[] GenerateSerialNumber(HashSet<BigInteger>? uniqueSerialNumbers = null)
        {
            // See https://www.rfc-editor.org/rfc/rfc5280#section-4.1.2.2
            //
            // A serial number MUST be:
            //
            //    * a non-negative integer
            //    * unique for that CA
            //    * <= 20 bytes
            //    * big-endian encoded
            //
            // To enforce uniqueness, we'll use BigInteger.
            //
            // Endianness here is dictated by .NET API's not the CPU architecture running this code.
            //
            //    * BigInteger's constructor requires an array of bytes interpreted as an integer in little-endian order.
            //    * CertificateRequest.Create(...) requires an array of bytes interpreted as an unsigned integer in big-endian order.
            byte[] bytes = new byte[20];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                while (true)
                {
                    rng.GetBytes(bytes);

                    // Treat the byte array as a little-endian integer.
                    // Ensure the number is non-negative by setting the highest bit of the first byte (little-endian) to 0.
                    bytes[bytes.Length - 1] &= 0x7F;

                    BigInteger serialNumber = new(bytes);

                    if (uniqueSerialNumbers is null || uniqueSerialNumbers.Add(serialNumber))
                    {
                        // Convert to big-endian.
                        Array.Reverse(bytes);

                        return bytes;
                    }
                }
            }
        }
    }
}
