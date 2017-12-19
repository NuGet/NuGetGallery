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
        public const string Leaf1Thumbprint = "b287c99e2c35226254a03ce20beffaa51504b8d586734731eaf66521a033ba59";
        public const string Leaf2Thumbprint = "bbd2309fc08d6b367bce9187ad696a9b2f25a013384f3979c4815dd4650736d0";

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
