// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    /// <summary>
    /// Represents a hash digest and the hash algorithm that produced it. This allows easy set comparisons of many
    /// hashes with varying hash algorithms.
    /// </summary>
    public class Hash : IEquatable<Hash>
    {
        public Hash(HashAlgorithmName algorithmName, byte[] digest)
        {
            if (digest == null)
            {
                throw new ArgumentNullException(nameof(digest));
            }

            AlgorithmName = algorithmName;
            HexDigest = BitConverter
                .ToString(digest)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        public HashAlgorithmName AlgorithmName { get; }
        public string HexDigest { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as Hash);
        }

        public bool Equals(Hash other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return AlgorithmName == other.AlgorithmName
                && HexDigest == other.HexDigest;
        }

        /// <summary>
        /// This method is auto-generated using Visual Studio.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 696079939;
                hashCode = hashCode * -1521134295 + AlgorithmName.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(HexDigest);
                return hashCode;
            }
        }
    }
}
