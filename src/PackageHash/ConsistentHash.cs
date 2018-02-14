// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Text;

namespace NuGet.Services.PackageHash
{
    public static class ConsistentHash
    {
        public static int DetermineBucket(string key, int bucketCount)
        {
            using (var hashAlgorithm = SHA256.Create())
            {
                var hashBytes = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(key));
                var reducedHash = 0;
                for (var i = 0; i < hashBytes.Length; i += sizeof(int))
                {
                    reducedHash ^= BitConverter.ToInt32(hashBytes, i);
                }

                return reducedHash % bucketCount;
            }
        }
    }
}
