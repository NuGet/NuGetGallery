// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Packaging;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public static class TestResources
    {
        private const string ResourceNamespace = "Validation.PackageSigning.ExtractAndValidateSignature.Tests.TestData";

        public const string UnsignedPackage = ResourceNamespace + ".TestUnsigned.1.0.0.nupkg";
        public const string SignedPackageLeaf1 = ResourceNamespace + ".TestSigned.leaf-1.1.0.0.nupkg";
        public const string SignedPackageLeaf2 = ResourceNamespace + ".TestSigned.leaf-2.2.0.0.nupkg";
        public const string Leaf1Thumbprint = "b67827d3556283e5d5d40befefe82e5b55a31f6ff7607ea946da9cc346d31e0a";
        public const string Leaf2Thumbprint = "d32b7e25c36bc85e4f58038f1aff90ffff2dd9a997deddd0dec05b3bc55fd884";

        public static PackageArchiveReader SignedPackageLeaf1Reader => LoadPackage(SignedPackageLeaf1);
        public static PackageArchiveReader SignedPackageLeaf2Reader => LoadPackage(SignedPackageLeaf2);

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

        public static PackageArchiveReader LoadPackage(string resourceName)
        {
            var resourceStream = GetResourceStream(resourceName);
            if (resourceStream == null)
            {
                return null;
            }

            return new PackageArchiveReader(resourceStream);
        }
    }
}
