// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Signing;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public static class TestResources
    {
        private const string ResourcePrefix = "Validation.PackageSigning.ProcessSignature.Tests.TestData.";
        public const string SignedPackageLeafId = "TestSigned.leaf";
        public const string SignedPackageLeaf1Version = "1.1.0";
        public const string SignedPackageLeaf1 = "TestSigned.leaf-1.1.0.0.nupkg.testdata";
        public const string SignedPackageLeaf2 = "TestSigned.leaf-2.2.0.0.nupkg.testdata";
        public const string UnsignedPackageId = "TestUnsigned";
        public const string UnsignedPackageVersion = "1.0.0";
        public const string UnsignedPackage = "TestUnsigned.1.0.0.nupkg.testdata";
        public const string Zip64Package = "Zip64Package.1.0.0.nupkg.testdata";
        public const string RepoSignedPackageLeafId = "TestRepoSigned.leaf";
        public const string RepoSignedPackageLeaf1 = "TestRepoSigned.leaf-1.1.0.0.nupkg.testdata";
        public const string RepoSignedPackageLeaf1Version = "1.1.0.0";
        public const string AuthorAndRepoSignedPackageLeafId = "TestAuthorAndRepoSigned.leaf";
        public const string AuthorAndRepoSignedPackageLeaf1 = "TestAuthorAndRepoSigned.leaf-1.1.0.0.nupkg.testdata";
        public const string AuthorAndRepoSignedPackageLeaf1Version = "1.1.0.0";

        /// <summary>
        /// This is the SHA-256 thumbprint of the root CA certificate for the signing certificate of <see cref="SignedPackageLeaf1"/>.
        /// </summary>
        public const string RootThumbprint = "557276839c961df211cf267b318d880568676efa41e8b62d9bb38752c1d6214d";

        /// <summary>
        /// This is the SHA-256 thumbprint of the signing certificate in <see cref="SignedPackageLeaf1"/>.
        /// </summary>
        public const string Leaf1Thumbprint = "4456d5d38709876dcd20ef3d7ba98bfd79fcaee91141d153a55f10841ef909c6";

        /// <summary>
        /// This is the SHA-256 thumbprint of the signing certificate in <see cref="SignedPackageLeaf2"/>.
        /// </summary>
        public const string Leaf2Thumbprint = "a8cc70dbbd8bc61410231805b690cca7c5a8d07553c1c49b299a6aabaeb7ff9a";

        /// <summary>
        /// This is the SHA-1 thumbprint of the signing certificate in <see cref="SignedPackageLeaf2"/>.
        /// </summary>
        public const string Leaf2Sha1Thumbprint = "8e1b5dadf388dee204bcfd27b53f00b585fdca07";

        /// <summary>
        /// This is the SHA-256 thumbprint of the timestamp certificate in <see cref="SignedPackageLeaf1"/>.
        /// </summary>
        public const string Leaf1TimestampThumbprint = "cf7ac17ad047ecd5fdc36822031b12d4ef078b6f2b4c5e6ba41f8ff2cf4bad67";

        /// <summary>
        /// The URL for the V3 service index that repository signed <see cref="RepoSignedPackageLeaf1"/> and <see cref="AuthorAndRepoSignedPackageLeaf1"/>.
        /// </summary>
        public const string V3ServiceIndexUrl = "https://example-service/v3/index.json";

        /// <summary>
        /// Buffer the resource stream into memory so the caller doesn't have to dispose.
        /// </summary>
        public static MemoryStream GetResourceStream(string resourceName)
        {
            var resourceStream = typeof(TestResources)
                .Assembly
                .GetManifestResourceStream(ResourcePrefix + resourceName);

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

        public static async Task<PrimarySignature> LoadPrimarySignatureAsync(string resourceName)
        {
            using (var package = LoadPackage(resourceName))
            {
                return await package.GetPrimarySignatureAsync(CancellationToken.None);
            }
        }
    }
}
