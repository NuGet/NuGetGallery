// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NuGetGallery.Services.Authentication
{
    public static class LegacyHasher
    {
        private const int SaltLengthInBytes = 16;

        public static string GenerateHash(string input, string hashAlgorithmId)
        {
            var saltBytes = new byte[SaltLengthInBytes];

            using (var cryptoProvider = new RNGCryptoServiceProvider())
            {
                cryptoProvider.GetNonZeroBytes(saltBytes);
            }

            var textBytes = Encoding.Unicode.GetBytes(input);

            var saltedTextBytes = new byte[saltBytes.Length + textBytes.Length];
            Array.Copy(saltBytes, saltedTextBytes, saltBytes.Length);
            Array.Copy(textBytes, 0, saltedTextBytes, saltBytes.Length, textBytes.Length);

            byte[] hashBytes;
            using (var hashAlgorithm = HashAlgorithm.Create(hashAlgorithmId))
            {
                hashBytes = hashAlgorithm.ComputeHash(saltedTextBytes);
            }

            var saltPlusHashBytes = new byte[saltBytes.Length + hashBytes.Length];
            Array.Copy(saltBytes, saltPlusHashBytes, saltBytes.Length);
            Array.Copy(hashBytes, 0, saltPlusHashBytes, saltBytes.Length, hashBytes.Length);

            var saltedHash = Convert.ToBase64String(saltPlusHashBytes);
            return saltedHash;
        }

        public static bool VerifyHash(string hash, string input, string hashAlgorithmId)
        {
            var saltPlusHashBytes = Convert.FromBase64String(hash);

            var saltBytes = saltPlusHashBytes.Take(SaltLengthInBytes).ToArray();
            var hashToValidateBytes = saltPlusHashBytes.Skip(SaltLengthInBytes).ToArray();

            var textBytes = Encoding.Unicode.GetBytes(input);

            var saltedTextBytes = new byte[saltBytes.Length + textBytes.Length];
            Array.Copy(saltBytes, saltedTextBytes, saltBytes.Length);
            Array.Copy(textBytes, 0, saltedTextBytes, saltBytes.Length, textBytes.Length);

            byte[] hashBytes;
            using (var hashAlgorithm = HashAlgorithm.Create(hashAlgorithmId))
            {
                hashBytes = hashAlgorithm.ComputeHash(saltedTextBytes);
            }

            for (int i = 0; i < hashBytes.Length; i++)
            {
                if (!hashBytes[i].Equals(hashToValidateBytes[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}