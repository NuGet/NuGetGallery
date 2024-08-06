// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace NuGetGallery.Infrastructure.Authentication
{
    /// <summary>
    /// This code is mostly copied from https://github.com/aspnet/Identity/blob/dev/src/Microsoft.AspNetCore.Identity/PasswordHasher.cs
    /// The algorithm: PBKDF2 with HMAC-SHA256, 128-bit salt, 256-bit subkey, 10000 iterations.
    /// </summary>
    public static class V3Hasher
    {
        private static readonly RNGCryptoServiceProvider DefaultRng = new RNGCryptoServiceProvider();

        private const int IterationCount = 10000;

        /// <summary>
        /// Returns a hashed representation of the supplied <paramref name="input"/>.
        /// </summary>
        /// <param name="input">The string to hash.</param>
        /// <returns>A hashed representation of the supplied<paramref name="input"/> Encoded to Base64 string.</returns>
        public static string GenerateHash(string input)
        {
            return Convert.ToBase64String(GenerateHashAsBytes(input));
        }

        /// <summary>
        /// Returns a hashed representation of the supplied <paramref name="input"/>.
        /// </summary>
        /// <param name="input">The string to hash.</param>
        /// <returns>A hashed representation of the supplied <paramref name="input"/>.</returns>
        public static byte[] GenerateHashAsBytes(string input)
        {
            return GenerateHashInternal(input, DefaultRng,
                prf: KeyDerivationPrf.HMACSHA256,
                iterCount: IterationCount,
                saltSize: 128 / 8,
                numBytesRequested: 256 / 8);
        }

        /// <summary>
        /// Returns a <see cref="bool"/> indicating the result of a hash comparison.
        /// </summary>
        /// <param name="hashedData">The hash value for a user's stored credential.</param>
        /// <param name="providedInput">The input supplied for comparison.</param>
        /// <returns>A <see cref="bool"/> indicating the result of a hash comparison.</returns>
        public static bool VerifyHash(byte[] hashedData, string providedInput)
        {
            if (hashedData == null)
            {
                throw new ArgumentNullException(nameof(hashedData));
            }

            if (providedInput == null)
            {
                throw new ArgumentNullException(nameof(providedInput));
            }

            // Read the format marker from the hashed credential
            if (hashedData.Length == 0)
            {
                return false;
            }

            // Verify format marker
            if (hashedData[0] != 0x01)
            {
                return false;
            }

            return VerifyHashInternal(hashedData, providedInput);
        }

        /// <summary>
        /// Returns a <see cref="bool"/> indicating the result of a hash comparison.
        /// </summary>
        /// <param name="hashedData">The hash value for a user's stored credential (Base64 encoded).</param>
        /// <param name="providedInput">The input supplied for comparison.</param>
        /// <returns>A <see cref="bool"/> indicating the result of a hash comparison.</returns>
        public static bool VerifyHash(string hashedData, string providedInput)
        {
            return VerifyHash(Convert.FromBase64String(hashedData), providedInput);
        }

        private static byte[] GenerateHashInternal(string input, RNGCryptoServiceProvider rng, KeyDerivationPrf prf, int iterCount, int saltSize, int numBytesRequested)
        {
            // Produce a version 3 (see comment above) text hash.
            byte[] salt = new byte[saltSize];
            rng.GetBytes(salt);
            byte[] subkey = KeyDerivation.Pbkdf2(input, salt, prf, iterCount, numBytesRequested);

            var outputBytes = new byte[13 + salt.Length + subkey.Length];
            outputBytes[0] = 0x01; // format marker
            WriteNetworkByteOrder(outputBytes, 1, (uint)prf);
            WriteNetworkByteOrder(outputBytes, 5, (uint)iterCount);
            WriteNetworkByteOrder(outputBytes, 9, (uint)saltSize);
            Buffer.BlockCopy(salt, 0, outputBytes, 13, salt.Length);
            Buffer.BlockCopy(subkey, 0, outputBytes, 13 + saltSize, subkey.Length);
            return outputBytes;
        }

        private static bool VerifyHashInternal(byte[] hashedData, string providedInput)
        {
            try
            {
                // Read header information
                KeyDerivationPrf prf = (KeyDerivationPrf)ReadNetworkByteOrder(hashedData, 1);
                int iterCount = (int)ReadNetworkByteOrder(hashedData, 5);
                int saltLength = (int)ReadNetworkByteOrder(hashedData, 9);

                // Read the salt: must be >= 128 bits
                if (saltLength < 128 / 8)
                {
                    return false;
                }
                byte[] salt = new byte[saltLength];
                Buffer.BlockCopy(hashedData, 13, salt, 0, salt.Length);

                // Read the subkey (the rest of the payload): must be >= 128 bits
                int subkeyLength = hashedData.Length - 13 - salt.Length;
                if (subkeyLength < 128 / 8)
                {
                    return false;
                }
                byte[] expectedSubkey = new byte[subkeyLength];
                Buffer.BlockCopy(hashedData, 13 + salt.Length, expectedSubkey, 0, expectedSubkey.Length);

                // Hash the incoming credential and verify it
                byte[] actualSubkey = KeyDerivation.Pbkdf2(providedInput, salt, prf, iterCount, subkeyLength);
                return ByteArraysEqual(actualSubkey, expectedSubkey);
            }
            catch
            {
                // This should never occur except in the case of a malformed payload, where
                // we might go off the end of the array. Regardless, a malformed payload
                // implies verification failed.
                return false;
            }
        }

        private static uint ReadNetworkByteOrder(byte[] buffer, int offset)
        {
            return ((uint)(buffer[offset + 0]) << 24)
                | ((uint)(buffer[offset + 1]) << 16)
                | ((uint)(buffer[offset + 2]) << 8)
                | ((uint)(buffer[offset + 3]));
        }
        private static void WriteNetworkByteOrder(byte[] buffer, int offset, uint value)
        {
            buffer[offset + 0] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)(value >> 0);
        }

        // Compares two byte arrays for equality. The method is specifically written so that the loop is not optimized.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a == null && b == null)
            {
                return true;
            }
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }
            var areSame = true;
            for (var i = 0; i < a.Length; i++)
            {
                areSame &= (a[i] == b[i]);
            }
            return areSame;
        }
    }
}