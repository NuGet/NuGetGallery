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
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Validation;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.X509.Store;
using Validation.PackageSigning.Core.Tests.Support;
using Xunit;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    public class SignaturePartsExtractorFacts
    {
        private const string BouncyCastleCollection = "Collection";

        private static readonly DateTime Leaf1TimestampValue = DateTime
            .Parse("2018-01-26T22:09:01.0000000Z")
            .ToUniversalTime();

        /// <summary>
        /// This contains the SHA-256 thumbprints of the test certificate chain as well as the time stamping
        /// authoring (TSA) chain. In this case, Symantec's TSA is used.
        /// </summary>
        private static readonly ExtractedCertificatesThumbprints Leaf1Certificates = new ExtractedCertificatesThumbprints
        {
            SignatureEndCertificate = new SubjectAndThumbprint(
                "CN=NUGET_DO_NOT_TRUST.leaf-1.test.test, OU=Test Organizational Unit Name, O=Test Organization Name, L=Redmond, S=WA, C=US",
                TestResources.Leaf1Thumbprint),
            SignatureParentCertificates = new[]
            {
                new SubjectAndThumbprint(
                    "CN=NUGET_DO_NOT_TRUST.intermediate.test.test, OU=Test Organizational Unit Name, O=Test Organization Name, L=Redmond, S=WA, C=US",
                    "7358e4597696b1d02e7aa2b3cf30a7cf154f2c8ff0710fd0dc3ace17e3784054"),
                new SubjectAndThumbprint(
                    "CN=NUGET_DO_NOT_TRUST.root.test.test, OU=Test Organizational Unit Name, O=Test Organization Name, L=Redmond, S=WA, C=US",
                    TestResources.RootThumbprint),
            },
            TimestampEndCertificate = new SubjectAndThumbprint(
                "CN=Symantec SHA256 TimeStamping Signer - G2, OU=Symantec Trust Network, O=Symantec Corporation, C=US",
                TestResources.Leaf1TimestampThumbprint),
            TimestampParentCertificates = new[]
            {
                new SubjectAndThumbprint(
                    "CN=Symantec SHA256 TimeStamping CA, OU=Symantec Trust Network, O=Symantec Corporation, C=US",
                    "f3516ddcc8afc808788bd8b0e840bda2b5e23c6244252ca3000bb6c87170402a"),
                new SubjectAndThumbprint(
                    "CN=VeriSign Universal Root Certification Authority, OU=\"(c) 2008 VeriSign, Inc. - For authorized use only\", OU=VeriSign Trust Network, O=\"VeriSign, Inc.\", C=US",
                    "2399561127a57125de8cefea610ddf2fa078b5c8067f4e828290bfb860e84b3c"),
            },
        };

        public class ExtractAsync
        {
            private readonly int _packageKey;
            private readonly CancellationToken _token;
            private readonly Mock<ICertificateStore> _certificateStore;
            private readonly List<X509Certificate2> _savedCertificates;
            private readonly Mock<IValidationEntitiesContext> _entitiesContext;
            private readonly Mock<ILogger<SignaturePartsExtractor>> _logger;
            private readonly SignaturePartsExtractor _target;

            public ExtractAsync()
            {
                _packageKey = 23;
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
                _entitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create<PackageSignature>());
                _entitiesContext
                    .Setup(x => x.TrustedTimestamps)
                    .Returns(DbSetMockFactory.Create<TrustedTimestamp>());

                _logger = new Mock<ILogger<SignaturePartsExtractor>>();

                _target = new SignaturePartsExtractor(
                    _certificateStore.Object,
                    _entitiesContext.Object,
                    _logger.Object);
            }

            [Fact]
            public async Task SaveSigningAndTimestampCertificates()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                VerifySavedCertificates(Leaf1Certificates, Leaf1TimestampValue);
            }

            [Fact]
            public async Task SavesToStorageBeforeDatabaseCommit()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
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
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                Assert.NotEqual(database, storage);
                Assert.Single(sequence, database);
                Assert.Equal(database, sequence.Last());
                Assert.Contains(storage, sequence);
            }

            [Fact]
            public async Task ProperlyInitializesEndCertificates()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                var endCertificates = _entitiesContext.Object.EndCertificates.ToList();
                foreach (var endCertificate in endCertificates)
                {

                    Assert.Null(endCertificate.LastVerificationTime);
                    Assert.Null(endCertificate.NextStatusUpdateTime);
                    Assert.Null(endCertificate.RevocationTime);
                    Assert.Equal(EndCertificateStatus.Unknown, endCertificate.Status);
                    Assert.NotEqual(default(EndCertificateUse), endCertificate.Use);
                    Assert.Null(endCertificate.StatusUpdateTime);
                    Assert.NotNull(endCertificate.Thumbprint);
                    Assert.Equal(64, endCertificate.Thumbprint.Length);
                }
            }

            [Fact]
            public async Task DoesNotDuplicateWhenDataAlreadyExist()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                await _target.ExtractAsync(_packageKey, signature, _token);
                AssignIds();
                _entitiesContext.ResetCalls();

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                VerifySavedCertificates(Leaf1Certificates, Leaf1TimestampValue);
                Assert.Equal(2, _entitiesContext.Object.EndCertificates.Count());
                Assert.Equal(4, _entitiesContext.Object.ParentCertificates.Count());
                Assert.Equal(4, _entitiesContext.Object.CertificateChainLinks.Count());
                Assert.Equal(1, _entitiesContext.Object.PackageSignatures.Count());
                Assert.Equal(1, _entitiesContext.Object.TrustedTimestamps.Count());
            }

            [Fact]
            public async Task DoesNotDuplicateWhenSomeDataAlreadyExist()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
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
                    Use = EndCertificateUse.CodeSigning,
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

                var existingPackageSignature = new PackageSignature
                {
                    Key = 1,
                    EndCertificate = existingEndCertificate,
                    EndCertificateKey = existingEndCertificate.Key,
                    Status = PackageSignatureStatus.Valid,
                    CreatedAt = new DateTime(2017, 1, 1, 8, 30, 0, DateTimeKind.Utc),
                    PackageKey = _packageKey,
                    TrustedTimestamps = new List<TrustedTimestamp>(),
                };

                _entitiesContext
                    .Setup(x => x.ParentCertificates)
                    .Returns(DbSetMockFactory.Create(existingParentCertificate));
                _entitiesContext
                    .Setup(x => x.EndCertificates)
                    .Returns(DbSetMockFactory.Create(existingEndCertificate));
                _entitiesContext
                    .Setup(x => x.CertificateChainLinks)
                    .Returns(DbSetMockFactory.Create(existingLink));
                _entitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature));

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                VerifySavedCertificates(Leaf1Certificates, Leaf1TimestampValue);
                Assert.Equal(2, _entitiesContext.Object.EndCertificates.Count());
                Assert.Equal(4, _entitiesContext.Object.ParentCertificates.Count());
                Assert.Equal(4, _entitiesContext.Object.CertificateChainLinks.Count());
                Assert.Equal(1, _entitiesContext.Object.PackageSignatures.Count());
                Assert.Equal(1, _entitiesContext.Object.TrustedTimestamps.Count());
                Assert.Equal(EndCertificateStatus.Good, existingEndCertificate.Status);
                Assert.Equal(PackageSignatureStatus.Valid, existingPackageSignature.Status);
            }

            [Fact]
            public async Task RejectsCertificateWithMultipleUses()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                var existingEndCertificate = new EndCertificate
                {
                    Key = 1,
                    Thumbprint = TestResources.Leaf1Thumbprint,
                    Status = EndCertificateStatus.Good,
                    Use = EndCertificateUse.Timestamping,
                    CertificateChainLinks = new List<CertificateChainLink>(),
                };

                _entitiesContext
                    .Setup(x => x.EndCertificates)
                    .Returns(DbSetMockFactory.Create(existingEndCertificate));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("The use of an end certificate cannot change.", ex.Message);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Empty(_savedCertificates);
            }

            [Fact]
            public async Task RejectsPackageWithMultipleSignatures()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                var existingPackageSignature1 = new PackageSignature
                {
                    Key = 1,
                    PackageKey = _packageKey,
                };

                var existingPackageSignature2 = new PackageSignature
                {
                    Key = 2,
                    PackageKey = _packageKey,
                };

                _entitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature1, existingPackageSignature2));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("There should never be more than one package signature per package.", ex.Message);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Empty(_savedCertificates);
            }

            [Fact]
            public async Task RejectsPackageWithChangedSigningCertificateThumbprint()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                var existingPackageSignature = new PackageSignature
                {
                    Key = 1,
                    PackageKey = _packageKey,
                    EndCertificate = new EndCertificate
                    {
                        Thumbprint = "something else",
                    }
                };

                _entitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("The thumbprint of the signature end certificate cannot change.", ex.Message);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Empty(_savedCertificates);
            }

            [Fact]
            public async Task RejectsPackageWithMultipleTimestamps()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                var existingPackageSignature = new PackageSignature
                {
                    Key = 1,
                    PackageKey = _packageKey,
                    EndCertificate = new EndCertificate
                    {
                        Thumbprint = TestResources.Leaf1Thumbprint,
                    },
                    TrustedTimestamps = new[]
                    {
                            new TrustedTimestamp(),
                            new TrustedTimestamp(),
                        },
                };

                _entitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("There should never be more than one trusted timestamp per package signature.", ex.Message);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Empty(_savedCertificates);
            }

            [Fact]
            public async Task RejectsPackageWithChangedTimestampCertificateThumbprint()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                var existingPackageSignature = new PackageSignature
                {
                    Key = 1,
                    PackageKey = _packageKey,
                    EndCertificate = new EndCertificate
                    {
                        Thumbprint = TestResources.Leaf1Thumbprint,
                    },
                    TrustedTimestamps = new[]
                    {
                            new TrustedTimestamp
                            {
                                EndCertificate = new EndCertificate
                                {
                                    Thumbprint = "something else",
                                },
                            },
                        },
                };

                _entitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("The thumbprint of the timestamp end certificate cannot change.", ex.Message);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Empty(_savedCertificates);
            }

            [Fact]
            public async Task RejectsPackageWithChangedTimestampValue()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                var existingPackageSignature = new PackageSignature
                {
                    Key = 1,
                    PackageKey = _packageKey,
                    EndCertificate = new EndCertificate
                    {
                        Thumbprint = TestResources.Leaf1Thumbprint,
                    },
                    TrustedTimestamps = new[]
                    {
                        new TrustedTimestamp
                        {
                            EndCertificate = new EndCertificate
                            {
                                Thumbprint = TestResources.Leaf1TimestampThumbprint,
                            },
                            Value = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        },
                    },
                };

                _entitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("The value of the trusted timestamp cannot change.", ex.Message);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Empty(_savedCertificates);
            }

            [Fact]
            public async Task IgnoreExtraCertificates()
            {
                // Arrange
                var originalSignature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                var unrelatedSignature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf2);
                var signature = AddCertificates(originalSignature.SignedCms, unrelatedSignature.SignedCms);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                VerifySavedCertificates(Leaf1Certificates, Leaf1TimestampValue);
                Assert.Equal(
                    Leaf1Certificates.Certificates.Count + 1,
                    signature.SignedCms.Certificates.Count + signature.Timestamps.Sum(x => x.SignedCms.Certificates.Count));
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

                var packageSignatures = _entitiesContext.Object.PackageSignatures.AsQueryable().ToList();
                for (var key = 1; key <= packageSignatures.Count; key++)
                {
                    var packageSignature = packageSignatures[key - 1];
                    packageSignature.Key = key;
                    foreach (var trustedTimestamp in packageSignature.TrustedTimestamps.ToList())
                    {
                        trustedTimestamp.PackageSignatureKey = key;
                    }

                    packageSignature.EndCertificateKey = packageSignature.EndCertificate.Key;
                }

                var trustedTimestamps = _entitiesContext.Object.TrustedTimestamps.AsQueryable().ToList();
                for (var key = 1; key <= trustedTimestamps.Count; key++)
                {
                    var trustedTimestamp = trustedTimestamps[key - 1];
                    trustedTimestamp.EndCertificateKey = trustedTimestamp.EndCertificate.Key;
                }
            }

            private void VerifySavedCertificates(ExtractedCertificatesThumbprints expected, DateTime timestampValue)
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

                Assert.Equal(EndCertificateUse.CodeSigning, signatureEndCertificate.Use);

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

                Assert.Equal(EndCertificateUse.Timestamping, timestampEndCertificate.Use);

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

                var packageSignature = Assert.Single(_entitiesContext.Object.PackageSignatures);
                Assert.Equal(_packageKey, packageSignature.PackageKey);
                Assert.NotEqual(default(DateTime), packageSignature.CreatedAt);
                Assert.Equal(expected.SignatureEndCertificate.Thumbprint, packageSignature.EndCertificate.Thumbprint);
                Assert.Same(signatureEndCertificate, packageSignature.EndCertificate);
                Assert.NotNull(packageSignature.TrustedTimestamps);

                var trustedTimestamp = Assert.Single(_entitiesContext.Object.TrustedTimestamps);
                Assert.Same(trustedTimestamp, packageSignature.TrustedTimestamps.Single());
                Assert.Same(packageSignature, trustedTimestamp.PackageSignature);
                Assert.Equal(expected.TimestampEndCertificate.Thumbprint, trustedTimestamp.EndCertificate.Thumbprint);
                Assert.Same(timestampEndCertificate, trustedTimestamp.EndCertificate);
                Assert.Equal(timestampValue, trustedTimestamp.Value);
                Assert.Equal(TrustedTimestampStatus.Valid, trustedTimestamp.Status);
            }
        }

        private static PrimarySignature AddCertificates(SignedCms destination, SignedCms source)
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

                return PrimarySignature.Load(writeStream.ToArray());
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
                .Concat(new[] { SignatureEndCertificate })
                .Concat(SignatureParentCertificates)
                .Concat(new[] { TimestampEndCertificate })
                .Concat(TimestampParentCertificates)
                .ToList();
        }
    }
}
