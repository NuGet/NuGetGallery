// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Packaging.Signing;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public static class TestResources
    {
        private const string ResourceNamespace = "Validation.PackageSigning.ExtractAndValidateSignature.Tests.TestData";

        public const string SignedPackageLeaf1 = ResourceNamespace + ".TestSigned.leaf-1.1.0.0.nupkg";
        public const string SignedPackageLeaf2 = ResourceNamespace + ".TestSigned.leaf-2.2.0.0.nupkg";
        public const string UnsignedPackage = ResourceNamespace + ".TestUnsigned.1.0.0.nupkg";
        public const string Zip64Package = ResourceNamespace + ".Zip64Package.1.0.0.nupkg";

        /// <summary>
        /// This is the SHA-256 thumbprint of the root CA certificate for the signing certificate of <see cref="SignedPackageLeaf1"/>.
        /// </summary>
        public const string RootThumbprint = "0e829fa17cfd9be513a41d9f205320f7d035f48d6c4cc7acbaa95f1744c1d6bb";

        /// <summary>
        /// This is the SHA-256 thumbprint of the signing certificate in <see cref="SignedPackageLeaf1"/>.
        /// </summary>
        public const string Leaf1Thumbprint = "56a23ed7c0ef80bd0269d4a3b41e3e2830243a9fc85594b6c311e27423df6023";

        /// <summary>
        /// This is the SHA-256 thumbprint of the signing certificate in <see cref="SignedPackageLeaf2"/>.
        /// </summary>
        public const string Leaf2Thumbprint = "cd177f02cb88f6e6fb6b0dd67d68559b101c3e100fb19ebf4db43d9d082674e1";

        /// <summary>
        /// This is the SHA-256 thumbprint of the timestamp certificate in <see cref="SignedPackageLeaf1"/>.
        /// </summary>
        public const string Leaf1TimestampThumbprint = "cf7ac17ad047ecd5fdc36822031b12d4ef078b6f2b4c5e6ba41f8ff2cf4bad67";

        public static SignedPackageArchive SignedPackageLeaf1Reader => LoadPackage(SignedPackageLeaf1);
        public static SignedPackageArchive SignedPackageLeaf2Reader => LoadPackage(SignedPackageLeaf2);

        /// <summary>
        /// Buffer the resource stream into memory so the caller doesn't have to dispose.
        /// </summary>
        public static MemoryStream GetResourceStream(string resourceName)
        {
            var resourceStream = typeof(TestResources)
                .Assembly
                .GetManifestResourceStream(resourceName);

            if (resourceStream == null)
            {
                return null;
            }

            var bufferedStream = new MemoryStream();
            using (resourceStream)
            {
                resourceStream.CopyTo(bufferedStream);
            }

            bufferedStream.Position = 0;
            return bufferedStream;
        }

        public static SignedPackageArchive LoadPackage(string resourceName)
        {
            var resourceStream = GetResourceStream(resourceName);
            if (resourceStream == null)
            {
                return null;
            }

            return new SignedPackageArchive(resourceStream, resourceStream);
        }
    }
}
