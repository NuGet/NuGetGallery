// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
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
using NuGet.Services.Validation;
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
        private static readonly ExtractedCertificatesThumbprints Leaf1Certificates = new ExtractedCertificatesThumbprints
        {
            SignatureParentCertificates = new[]
            {
                new SubjectAndThumbprint(
                    "CN=NUGET_DO_NOT_TRUST.root.test.test, OU=Test Organizational Unit Name, O=Test Organization Name, L=Redmond, S=WA, C=US",
                    TestResources.RootThumbprint),
                new SubjectAndThumbprint(
                    "CN=NUGET_DO_NOT_TRUST.intermediate.test.test, OU=Test Organizational Unit Name, O=Test Organization Name, L=Redmond, S=WA, C=US",
                    "d5949445cde4d80bc0c857dddb8520114a146d73de081a77404b0c17dda6a4b4"),
            },
            SignatureEndCertificate = new SubjectAndThumbprint(
                "CN=NUGET_DO_NOT_TRUST.leaf-1.test.test, OU=Test Organizational Unit Name, O=Test Organization Name, L=Redmond, S=WA, C=US",
                TestResources.Leaf1Thumbprint),
            TimestampParentCertificates = new[]
            {
                new SubjectAndThumbprint(
                    "CN=Symantec SHA256 TimeStamping CA, OU=Symantec Trust Network, O=Symantec Corporation, C=US",
                    "f3516ddcc8afc808788bd8b0e840bda2b5e23c6244252ca3000bb6c87170402a")
            },
            TimestampEndCertificate = new SubjectAndThumbprint(
                "CN=Symantec SHA256 TimeStamping Signer - G2, OU=Symantec Trust Network, O=Symantec Corporation, C=US",
                "cf7ac17ad047ecd5fdc36822031b12d4ef078b6f2b4c5e6ba41f8ff2cf4bad67"),
        };

        public class ExtractAsync
        {
            private readonly Mock<ISignedPackageReader> _packageMock;
            private readonly CancellationToken _token;
            private readonly Mock<ICertificateStore> _certificateStore;
            private readonly List<X509Certificate2> _savedCertificates;
            private readonly Mock<IValidationEntitiesContext> _entitiesContext;
            private readonly SignaturePartsExtractor _target;

            public ExtractAsync()
            {
                _packageMock = new Mock<ISignedPackageReader>();
                _token = CancellationToken.None;
                _savedCertificates = new List<X509Certificate2>();

                _certificateStore = new Mock<ICertificateStore>();
                _certificateStore
                    .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns<string, CancellationToken>((t, _) => Task.FromResult(_savedCertificates.Any(x => x.ComputeSHA256Thumbprint() == t)));

                _certificateStore
                    .Setup(x => x.SaveAsync(It.IsAny<X509Certificate2>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Callback<X509Certificate2, CancellationToken>((cert, _) => _savedCertificates.Add(cert));

                _entitiesContext = new Mock<IValidationEntitiesContext>();
                _entitiesContext
                    .Setup(x => x.ParentCertificates)
                    .Returns(DbSetMockFactory.Create<ParentCertificate>());
                _entitiesContext
                    .Setup(x => x.EndCertificates)
                    .Returns(DbSetMockFactory.Create<EndCertificate>());
                _entitiesContext
                    .Setup(x => x.CertificateChainLinks)
                    .Returns(DbSetMockFactory.Create<CertificateChainLink>());

                _target = new SignaturePartsExtractor(
                    _certificateStore.Object,
                    _entitiesContext.Object);
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
            public async Task SavesToStorageBeforeDatabaseCommit()
            {
                // Arrange
                using (var package = TestResources.LoadPackage(TestResources.SignedPackageLeaf1))
                {
                    const string database = "database";
                    const string storage = "storage";
                    var sequence = new List<string>();
                    _entitiesContext
                        .Setup(x => x.SaveChangesAsync())
                        .ReturnsAsync(0)
                        .Callback(() => sequence.Add(database));
                    _certificateStore
                        .Setup(x => x.SaveAsync(It.IsAny<X509Certificate2>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask)
                        .Callback(() => sequence.Add(storage));

                    // Act
                    await _target.ExtractAsync(package, _token);

                    // Assert
                    Assert.NotEqual(database, storage);
                    Assert.Single(sequence, database);
                    Assert.Equal(database, sequence.Last());
                    Assert.Contains(storage, sequence);
                }
            }

            [Fact]
            public async Task ProperlyInitializesEndCertificates()
            {
                // Arrange
                using (var package = TestResources.LoadPackage(TestResources.SignedPackageLeaf1))
                {
                    // Act
                    await _target.ExtractAsync(package, _token);

                    // Assert
                    var endCertificates = _entitiesContext.Object.EndCertificates.ToList();
                    foreach (var endCertificate in endCertificates)
                    {
                        
                        Assert.Null(endCertificate.LastVerificationTime);
                        Assert.Null(endCertificate.NextStatusUpdateTime);
                        Assert.Null(endCertificate.RevocationTime);
                        Assert.Equal(EndCertificateStatus.Unknown, endCertificate.Status);
                        Assert.Null(endCertificate.StatusUpdateTime);
                        Assert.NotNull(endCertificate.Thumbprint);
                        Assert.Equal(64, endCertificate.Thumbprint.Length);
                    }
                }
            }

            [Fact]
            public async Task DoesNotDuplicateWhenAllLinksAlreadyExist()
            {
                // Arrange
                using (var package = TestResources.LoadPackage(TestResources.SignedPackageLeaf1))
                {
                    await _target.ExtractAsync(package, _token);
                    AssignIds();
                    _entitiesContext.ResetCalls();

                    // Act
                    await _target.ExtractAsync(package, _token);
                    
                    // Assert
                    VerifySavedCertificates(Leaf1Certificates);
                    Assert.Equal(2, _entitiesContext.Object.EndCertificates.Count());
                    Assert.Equal(3, _entitiesContext.Object.ParentCertificates.Count());
                    Assert.Equal(3, _entitiesContext.Object.CertificateChainLinks.Count());
                }
            }

            [Fact]
            public async Task DoesNotDuplicateWhenSomeLinksAlreadyExist()
            {
                // Arrange
                using (var package = TestResources.LoadPackage(TestResources.SignedPackageLeaf1))
                {
                    var existingParentCertificate = new ParentCertificate
                    {
                        Key = 1,
                        Thumbprint = TestResources.RootThumbprint,
                        CertificateChainLinks = new List<CertificateChainLink>(),
                    };
                    var existingEndCertificate = new EndCertificate
                    {
                        Key = 1,
                        Thumbprint = TestResources.Leaf1Thumbprint,
                        Status = EndCertificateStatus.Good, // Different than the default.
                        CertificateChainLinks = new List<CertificateChainLink>(),
                    };
                    var existingLink = new CertificateChainLink
                    {
                        ParentCertificate = existingParentCertificate,
                        ParentCertificateKey = existingParentCertificate.Key,
                        EndCertificate = existingEndCertificate,
                        EndCertificateKey = existingEndCertificate.Key,
                    };
                    existingParentCertificate.CertificateChainLinks.Add(existingLink);
                    existingEndCertificate.CertificateChainLinks.Add(existingLink);

                    _entitiesContext
                        .Setup(x => x.ParentCertificates)
                        .Returns(DbSetMockFactory.Create(existingParentCertificate));
                    _entitiesContext
                        .Setup(x => x.EndCertificates)
                        .Returns(DbSetMockFactory.Create(existingEndCertificate));
                    _entitiesContext
                        .Setup(x => x.CertificateChainLinks)
                        .Returns(DbSetMockFactory.Create(existingLink));

                    // Act
                    await _target.ExtractAsync(package, _token);

                    // Assert
                    VerifySavedCertificates(Leaf1Certificates);
                    Assert.Equal(2, _entitiesContext.Object.EndCertificates.Count());
                    Assert.Equal(3, _entitiesContext.Object.ParentCertificates.Count());
                    Assert.Equal(3, _entitiesContext.Object.CertificateChainLinks.Count());
                    Assert.Equal(EndCertificateStatus.Good, existingEndCertificate.Status);
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
                        Leaf1Certificates.Certificates.Count + 1,
                        signature.SignedCms.Certificates.Count + signature.Timestamps.Sum(x => x.SignedCms.Certificates.Count));
                }
            }

            private void AssignIds()
            {
                var endCertificates = _entitiesContext.Object.EndCertificates.AsQueryable().ToList();
                for (var key = 1; key <= endCertificates.Count; key++)
                {
                    endCertificates[key - 1].Key = key;
                    foreach (var link in endCertificates[key - 1].CertificateChainLinks.ToList())
                    {
                        link.EndCertificateKey = key;
                    }
                }

                var parentCertificates = _entitiesContext.Object.ParentCertificates.AsQueryable().ToList();
                for (var key = 1; key <= parentCertificates.Count; key++)
                {
                    parentCertificates[key - 1].Key = key;
                    foreach (var link in parentCertificates[key - 1].CertificateChainLinks.ToList())
                    {
                        link.ParentCertificateKey = key;
                    }
                }
            }

            private void VerifySavedCertificates(ExtractedCertificatesThumbprints expected)
            {
                // Assert the certificates saved to the store.
                Assert.Equal(expected.Certificates.Count, _savedCertificates.Count);
                for (var i = 0; i < _savedCertificates.Count; i++)
                {
                    var subject = _savedCertificates[i].Subject;
                    var thumbprint = _savedCertificates[i].ComputeSHA256Thumbprint();
                    Assert.Equal(expected.Certificates[i].Subject, subject);
                    Assert.Equal(expected.Certificates[i].Thumbprint, thumbprint);
                }

                // Assert the certificates saved to the database.
                var signatureEndCertificate = Assert.Single(_entitiesContext
                    .Object
                    .EndCertificates
                    .Where(x => x.Thumbprint == expected.SignatureEndCertificate.Thumbprint));

                Assert.Equal(
                    expected
                        .SignatureParentCertificates
                        .Select(x => x.Thumbprint)
                        .OrderBy(x => x),
                     signatureEndCertificate
                        .CertificateChainLinks
                        .Select(x => x.ParentCertificate.Thumbprint)
                        .OrderBy(x => x));

                var timestampEndCertificate = Assert.Single(_entitiesContext
                    .Object
                    .EndCertificates
                    .Where(x => x.Thumbprint == expected.TimestampEndCertificate.Thumbprint));

                Assert.Equal(
                    expected
                        .TimestampParentCertificates
                        .Select(x => x.Thumbprint)
                        .OrderBy(x => x),
                     timestampEndCertificate
                        .CertificateChainLinks
                        .Select(x => x.ParentCertificate.Thumbprint)
                        .OrderBy(x => x));

                _entitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
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

        private class ExtractedCertificatesThumbprints
        {
            public IReadOnlyList<SubjectAndThumbprint> SignatureParentCertificates { get; set; }
            public SubjectAndThumbprint SignatureEndCertificate { get; set; }
            public IReadOnlyList<SubjectAndThumbprint> TimestampParentCertificates { get; set; }
            public SubjectAndThumbprint TimestampEndCertificate { get; set; }
            public IReadOnlyList<SubjectAndThumbprint> Certificates => Enumerable
                .Empty<SubjectAndThumbprint>()
                .Concat(SignatureParentCertificates)
                .Concat(new[] { SignatureEndCertificate })
                .Concat(TimestampParentCertificates)
                .Concat(new[] { TimestampEndCertificate })
                .ToList();
        }
    }
}
