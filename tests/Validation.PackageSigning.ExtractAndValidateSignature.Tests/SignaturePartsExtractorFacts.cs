// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.ExtractAndValidateSignature;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.X509.Store;
using Xunit;

namespace Validation.PackageSigning.ExtractAndValidateSignature.Tests
{
    public class SignaturePartsExtractorFacts
    {
        private const string BouncyCastleCollection = "Collection";

        /// <summary>
        /// This contains the SHA-256 thumbprints of the test certificate chain as well as the time stamping
        /// authoring (TSA) chain. In this case, Symantec's TSA is used.
        /// </summary>
        private static readonly IReadOnlyList<SubjectAndThumbprint> Leaf1Certificates = new[]
        {
            new SubjectAndThumbprint(
                "CN=NUGET_DO_NOT_TRUST.root.test.test, OU=Test Organizational Unit Name, O=Test Organization Name, L=Redmond, S=WA, C=US",
                "0e829fa17cfd9be513a41d9f205320f7d035f48d6c4cc7acbaa95f1744c1d6bb"),
            new SubjectAndThumbprint(
                "CN=NUGET_DO_NOT_TRUST.intermediate.test.test, OU=Test Organizational Unit Name, O=Test Organization Name, L=Redmond, S=WA, C=US",
                "d5949445cde4d80bc0c857dddb8520114a146d73de081a77404b0c17dda6a4b4"),
            new SubjectAndThumbprint(
                "CN=NUGET_DO_NOT_TRUST.leaf-1.test.test, OU=Test Organizational Unit Name, O=Test Organization Name, L=Redmond, S=WA, C=US",
                TestResources.Leaf1Thumbprint),
            new SubjectAndThumbprint(
                "CN=Symantec SHA256 TimeStamping CA, OU=Symantec Trust Network, O=Symantec Corporation, C=US",
                "f3516ddcc8afc808788bd8b0e840bda2b5e23c6244252ca3000bb6c87170402a"),
            new SubjectAndThumbprint(
                "CN=Symantec SHA256 TimeStamping Signer - G2, OU=Symantec Trust Network, O=Symantec Corporation, C=US",
                "cf7ac17ad047ecd5fdc36822031b12d4ef078b6f2b4c5e6ba41f8ff2cf4bad67"),
        };

        public class ExtractAsync
        {
            private readonly Mock<ISignedPackageReader> _packageMock;
            private readonly CancellationToken _token;
            private readonly Mock<ICertificateStore> _certificateStore;
            private readonly List<X509Certificate2> _savedCertificates;
            private readonly SignaturePartsExtractor _target;

            public ExtractAsync()
            {
                _packageMock = new Mock<ISignedPackageReader>();
                _token = CancellationToken.None;
                _savedCertificates = new List<X509Certificate2>();

                _certificateStore = new Mock<ICertificateStore>();
                _certificateStore
                    .Setup(x => x.SaveAsync(It.IsAny<X509Certificate2>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Callback<X509Certificate2, CancellationToken>((cert, _) => _savedCertificates.Add(cert));
                
                _target = new SignaturePartsExtractor(_certificateStore.Object);
            }

            [Fact]
            public async Task RejectsUnsignedPackages()
            {
                // Arrange
                _packageMock
                    .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                // Act & Assert
                var ex = await Assert.ThrowsAsync<ArgumentException>(
                    () => _target.ExtractAsync(_packageMock.Object, _token));
                Assert.Contains("The provided package reader must refer to a signed package.", ex.Message);
            }

            [Fact]
            public async Task SaveSigningAndTimestampCertificates()
            {
                // Arrange
                using (var package = TestResources.LoadPackage(TestResources.SignedPackageLeaf1))
                {
                    // Act
                    await _target.ExtractAsync(package, _token);

                    // Assert
                    VerifySavedCertificates(Leaf1Certificates);
                }
            }

            [Fact]
            public async Task IgnoreExtraCertificates()
            {
                // Arrange
                using (var package = TestResources.LoadPackage(TestResources.SignedPackageLeaf1))
                using (var unrelatedPackage = TestResources.LoadPackage(TestResources.SignedPackageLeaf2))
                {
                    var originalSignatures = await package.GetSignaturesAsync(_token);
                    var unrelatedSignatures = await unrelatedPackage.GetSignaturesAsync(_token);

                    var signature = AddCertificates(originalSignatures[0].SignedCms, unrelatedSignatures[0].SignedCms);

                    _packageMock
                        .Setup(x => x.IsSignedAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);
                    _packageMock
                        .Setup(x => x.GetSignaturesAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new[] { signature });

                    // Act
                    await _target.ExtractAsync(_packageMock.Object, _token);

                    // Assert
                    VerifySavedCertificates(Leaf1Certificates);
                    Assert.Equal(
                        Leaf1Certificates.Count + 1,
                        signature.SignedCms.Certificates.Count + signature.Timestamps.Sum(x => x.SignedCms.Certificates.Count));
                }
            }

            private void VerifySavedCertificates(IReadOnlyList<SubjectAndThumbprint> expected)
            {
                Assert.Equal(expected.Count, _savedCertificates.Count);
                for (var i = 0; i < _savedCertificates.Count; i++)
                {
                    var subject = _savedCertificates[i].Subject;
                    var thumbprint = _savedCertificates[i].ComputeSHA256Thumbprint();
                    Assert.Equal(expected[i].Subject, subject);
                    Assert.Equal(expected[i].Thumbprint, thumbprint);
                }
            }
        }

        private static Signature AddCertificates(SignedCms destination, SignedCms source)
        {
            using (var readStream = new MemoryStream(destination.Encode()))
            using (var writeStream = new MemoryStream())
            {
                var certificates = GetBouncyCastleCertificates(destination)
                    .Concat(GetBouncyCastleCertificates(source))
                    .Distinct()
                    .ToList();
                var certificateStore = X509StoreFactory.Create(
                    "Certificate/" + BouncyCastleCollection,
                    new X509CollectionStoreParameters(certificates));

                var crlStore = new CmsSignedData(destination.Encode()).GetCrls(BouncyCastleCollection);
                var attributeCertificateStore = new CmsSignedData(destination.Encode()).GetAttributeCertificates(BouncyCastleCollection);

                CmsSignedDataParser.ReplaceCertificatesAndCrls(
                    readStream,
                    certificateStore,
                    crlStore,
                    attributeCertificateStore,
                    writeStream);

                return Signature.Load(writeStream.ToArray());
            }
        }

        private static List<Org.BouncyCastle.X509.X509Certificate> GetBouncyCastleCertificates(SignedCms signedCms)
        {
            return new CmsSignedData(signedCms.Encode())
                .GetCertificates(BouncyCastleCollection)
                .GetMatches(selector: null)
                .Cast<Org.BouncyCastle.X509.X509Certificate>()
                .ToList();
        }
    }
}
