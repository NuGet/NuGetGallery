// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Web;

namespace NuGetGallery
{
    public static class CryptographyService
    {
        public static string GenerateHash(
            Stream input,
            string hashAlgorithmId)
        {
            input.Position = 0;

            byte[] hashBytes;

            using (var hashAlgorithm = HashAlgorithm.Create(hashAlgorithmId))
            {
                hashBytes = hashAlgorithm.ComputeHash(input);
            }

            var hash = Convert.ToBase64String(hashBytes);
            return hash;
        }

#if NETFRAMEWORK
        public static string GenerateToken()
        {
            var data = new byte[0x10];

            using (var crypto = new RNGCryptoServiceProvider())
            {
                crypto.GetBytes(data);

                return HttpServerUtility.UrlTokenEncode(data);
            }
        }
#endif
    }
}