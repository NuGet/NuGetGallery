// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Common;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using Xunit;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class HashFacts
    {
        public class TheEqualsMethod
        {
            [Fact]
            public void ConsidersDigest()
            {
                // Arrange
                var hashA = MakeHash(HashAlgorithmName.SHA256, "A");
                var hashB = MakeHash(HashAlgorithmName.SHA256, "B");

                // Act & Assert
                Assert.False(
                    hashA.Equals(hashB),
                    "The two hashes should not be equal since they have different digests.");
            }

            [Fact]
            public void ConsidersAlgorithm()
            {
                // Arrange
                var hashA = new Hash(HashAlgorithmName.SHA256, new byte[0]);
                var hashB = new Hash(HashAlgorithmName.SHA384, new byte[0]);

                // Act & Assert
                Assert.False(
                    hashA.Equals(hashB),
                    "The two hashes should not be equal since they have different algorithms.");
            }
        }

        public class TheHexDigestProperty
        {
            [Fact]
            public void ReturnsTheProvidedBytesAsHex()
            {
                // Arrange
                var bytes = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
                var expected = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
                var hash = new Hash(HashAlgorithmName.SHA256, bytes);

                // Act & Assert
                Assert.Equal(expected, hash.HexDigest);
            }
        }

        public class TheGetHashCodeMethod
        {
            [Fact]
            public void AllowsSetOperations()
            {
                var expected = new List<Hash>
                {
                    MakeHash(HashAlgorithmName.SHA256, "A"),
                    MakeHash(HashAlgorithmName.SHA384, "A"),
                    MakeHash(HashAlgorithmName.SHA512, "A"),
                    MakeHash(HashAlgorithmName.SHA256, "B"),
                    MakeHash(HashAlgorithmName.SHA512, "C"),
                };
                var actual = new HashSet<Hash>(new[]
                {
                    MakeHash(HashAlgorithmName.SHA256, "A"),
                    MakeHash(HashAlgorithmName.SHA256, "A"), // duplicate
                    MakeHash(HashAlgorithmName.SHA256, "A"), // duplicate
                    MakeHash(HashAlgorithmName.SHA384, "A"),
                    MakeHash(HashAlgorithmName.SHA384, "A"), // duplicate
                    MakeHash(HashAlgorithmName.SHA512, "A"),
                    MakeHash(HashAlgorithmName.SHA256, "B"),
                    MakeHash(HashAlgorithmName.SHA512, "C"),
                });

                Assert.Equal(
                    expected.OrderBy(x => x.AlgorithmName).ThenBy(x => x.HexDigest),
                    actual.OrderBy(x => x.AlgorithmName).ThenBy(x => x.HexDigest));
            }
        }

        private static Hash MakeHash(HashAlgorithmName algorithm, string hashInput)
        {
            var digest = CryptoHashUtility.ComputeHash(algorithm, Encoding.ASCII.GetBytes(hashInput));
            return new Hash(algorithm, digest);
        }
    }
}
