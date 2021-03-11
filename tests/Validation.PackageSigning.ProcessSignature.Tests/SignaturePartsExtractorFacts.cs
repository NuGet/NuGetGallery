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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.PackageSigning;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.X509.Store;
using Tests.ContextHelpers;
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

        private static readonly DateTime RepoSignedLeaf1TimestampValue = DateTime
            .Parse("2018-03-21T17:55:33.0000000Z")
            .ToUniversalTime();

        private static readonly DateTime AuthorAndRepoSignedPrimaryTimestampValue = DateTime
            .Parse("2018-03-21T17:55:50.0000000Z")
            .ToUniversalTime();

        private static readonly DateTime AuthorAndRepoSignedCounterTimestampValue = DateTime
            .Parse("2018-03-21T17:56:00.0000000Z")
            .ToUniversalTime();

        private static string Leaf1Cn = "NUGET_DO_NOT_TRUST.leaf-1.test.test";
        private static string Leaf1IssuerCn = "NUGET_DO_NOT_TRUST.intermediate.test.test";

        /// <summary>
        /// This contains the SHA-256 thumbprints of the test certificate chain as well as the time stamping
        /// authoring (TSA) chain. In this case, Symantec's TSA is used.
        /// </summary>
        private static readonly ExtractedCertificatesThumbprints Leaf1Certificates = new ExtractedCertificatesThumbprints
        {
            PrimarySignature = new SignatureCertificateThumprints
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
            },
        };

        private static readonly ExtractedCertificatesThumbprints AuthorAndRepoSignedCertificates = new ExtractedCertificatesThumbprints
        {
            PrimarySignature = Leaf1Certificates.PrimarySignature,
            Countersignature = new SignatureCertificateThumprints
            {
                SignatureEndCertificate = new SubjectAndThumbprint(
                "CN=NUGET_DO_NOT_TRUST.leaf-2.test.test, OU=Test Organizational Unit Name, O=Test Organization Name, L=Redmond, S=WA, C=US",
                TestResources.Leaf2Thumbprint),
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
            },
        };

        public class ExtractAsync
        {
            private readonly int _packageKey;
            private readonly CancellationToken _token;
            private readonly Mock<ICertificateStore> _certificateStore;
            private readonly List<X509Certificate2> _savedCertificates;
            private readonly Mock<IValidationEntitiesContext> _validationEntitiesContext;
            private readonly Mock<IOptionsSnapshot<ProcessSignatureConfiguration>> _configAccessor;
            private readonly ProcessSignatureConfiguration _config;
            private readonly Mock<ILogger<SignaturePartsExtractor>> _logger;
            private readonly SignaturePartsExtractor _target;
            private readonly Mock<IEntitiesContext> _galleryEntitiesContext;

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
                    .Callback<X509Certificate2, CancellationToken>((cert, _) => _savedCertificates.Add(new X509Certificate2(cert.RawData)));

                _validationEntitiesContext = new Mock<IValidationEntitiesContext>();
                _validationEntitiesContext.Mock();

                _galleryEntitiesContext = new Mock<IEntitiesContext>();
                _galleryEntitiesContext.Mock();

                _configAccessor = new Mock<IOptionsSnapshot<ProcessSignatureConfiguration>>();
                _config = new ProcessSignatureConfiguration
                {
                    CommitRepositorySignatures = true
                };

                _configAccessor.Setup(a => a.Value).Returns(_config);

                _logger = new Mock<ILogger<SignaturePartsExtractor>>();

                _target = new SignaturePartsExtractor(
                    _certificateStore.Object,
                    _validationEntitiesContext.Object,
                    _galleryEntitiesContext.Object,
                    _configAccessor.Object,
                    _logger.Object);
            }

            [Fact]
            public async Task IgnoresSubjectAndIssuerThatAreTooLong()
            {
                // Arrange
                var maxLength = Math.Min(
                    Leaf1Certificates.PrimarySignature.SignatureEndCertificate.Subject.Length - 1,
                    Leaf1Certificates.PrimarySignature.SignatureParentCertificates[0].Subject.Length - 1);
                _config.MaxCertificateStringLength = maxLength;
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                var certificate = signature.SignerInfo.Certificate;
                var certificateRecord = new Certificate
                {
                    Thumbprint = certificate.ComputeSHA256Thumbprint(),
                };
                _galleryEntitiesContext.Object.Certificates.Add(certificateRecord);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                Assert.Equal(certificate.NotAfter.ToUniversalTime(), certificateRecord.Expiration);
                Assert.Null(certificateRecord.Subject);
                Assert.Null(certificateRecord.Issuer);
                Assert.Equal(Leaf1Cn, certificateRecord.ShortSubject);
                Assert.Equal(Leaf1IssuerCn, certificateRecord.ShortIssuer);
                var single = Assert.Single(_galleryEntitiesContext.Object.Certificates);
                Assert.Same(certificateRecord, single);
                _galleryEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task IgnoresShortSubjectAndShortIssuerThatAreTooLong()
            {
                // Arrange
                var maxLength = Math.Min(Leaf1Cn.Length - 1, Leaf1IssuerCn.Length - 1);
                _config.MaxCertificateStringLength = maxLength;
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                var certificate = signature.SignerInfo.Certificate;
                var certificateRecord = new Certificate
                {
                    Thumbprint = certificate.ComputeSHA256Thumbprint(),
                };
                _galleryEntitiesContext.Object.Certificates.Add(certificateRecord);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                Assert.Equal(certificate.NotAfter.ToUniversalTime(), certificateRecord.Expiration);
                Assert.Null(certificateRecord.Subject);
                Assert.Null(certificateRecord.Issuer);
                Assert.Null(certificateRecord.ShortSubject);
                Assert.Null(certificateRecord.ShortIssuer);
                var single = Assert.Single(_galleryEntitiesContext.Object.Certificates);
                Assert.Same(certificateRecord, single);
                _galleryEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task UpdatesCertificateDetailsInGalleryForMatchingAuthorCertificate()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                var certificate = signature.SignerInfo.Certificate;
                var certificateRecord = new Certificate
                {
                    Thumbprint = certificate.ComputeSHA256Thumbprint(),
                };
                _galleryEntitiesContext.Object.Certificates.Add(certificateRecord);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                Assert.Equal(certificate.NotAfter.ToUniversalTime(), certificateRecord.Expiration);
                Assert.Equal(certificate.Subject, certificateRecord.Subject);
                Assert.Equal(certificate.Issuer, certificateRecord.Issuer);
                Assert.Equal(Leaf1Cn, certificateRecord.ShortSubject);
                Assert.Equal(Leaf1IssuerCn, certificateRecord.ShortIssuer);
                var single = Assert.Single(_galleryEntitiesContext.Object.Certificates);
                Assert.Same(certificateRecord, single);
                _galleryEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);
            }

            [Fact]
            public async Task DoesNotUpdateCertificateDetailsForRepositoryPrimarySignature()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.RepoSignedPackageLeaf1);
                var certificate = signature.SignerInfo.Certificate;
                var certificateRecord = new Certificate
                {
                    Thumbprint = certificate.ComputeSHA256Thumbprint(),
                };
                _galleryEntitiesContext.Object.Certificates.Add(certificateRecord);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                Assert.Null(certificateRecord.Expiration);
                Assert.Null(certificateRecord.Subject);
                Assert.Null(certificateRecord.Issuer);
                Assert.Null(certificateRecord.ShortSubject);
                Assert.Null(certificateRecord.ShortIssuer);
                Assert.Single(_galleryEntitiesContext.Object.Certificates);
                _galleryEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task DoesNotUpdateCertificateDetailsWhenNoMatchingRecordExists()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                Assert.Empty(_galleryEntitiesContext.Object.Certificates.ToArray());
                _galleryEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task DoesNotUpdateCertificateDetailsWhenNoDataHasChanged()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                var certificate = signature.SignerInfo.Certificate;
                var certificateRecord = new Certificate
                {
                    Expiration = certificate.NotAfter.ToUniversalTime(),
                    Subject = certificate.Subject,
                    Issuer = certificate.Issuer,
                    ShortSubject = Leaf1Cn,
                    ShortIssuer = Leaf1IssuerCn,
                    Thumbprint = certificate.ComputeSHA256Thumbprint(),
                };
                _galleryEntitiesContext.Object.Certificates.Add(certificateRecord);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                Assert.Equal(certificate.NotAfter.ToUniversalTime(), certificateRecord.Expiration);
                Assert.Equal(certificate.Subject, certificateRecord.Subject);
                Assert.Equal(certificate.Issuer, certificateRecord.Issuer);
                Assert.Equal(Leaf1Cn, certificateRecord.ShortSubject);
                Assert.Equal(Leaf1IssuerCn, certificateRecord.ShortIssuer);
                Assert.Single(_galleryEntitiesContext.Object.Certificates);
                _galleryEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Never);
            }

            [Fact]
            public async Task SaveSigningAndTimestampCertificatesForAuthorPrimarySignature()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                VerifyExtractedInformation(Leaf1Certificates, Leaf1TimestampValue, PackageSignatureType.Author);
            }

            [Fact]
            public async Task SaveSigningAndTimestampCertificatesForRepositoryPrimarySignature()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.RepoSignedPackageLeaf1);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                VerifyExtractedInformation(Leaf1Certificates, RepoSignedLeaf1TimestampValue, PackageSignatureType.Repository);
            }

            [Fact]
            public async Task SaveSigningAndTimestampCertificatesForAuthorAndReposignedPackage()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.AuthorAndRepoSignedPackageLeaf1);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                VerifyStoredCertificates(AuthorAndRepoSignedCertificates);
                VerifyPackageSignatureRecord(AuthorAndRepoSignedCertificates.PrimarySignature, AuthorAndRepoSignedPrimaryTimestampValue, PackageSignatureType.Author);
                VerifyPackageSignatureRecord(AuthorAndRepoSignedCertificates.Countersignature, AuthorAndRepoSignedCounterTimestampValue, PackageSignatureType.Repository);
            }

            [Fact]
            public async Task SavesToStorageBeforeDatabaseCommit()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);
                const string database = "database";
                const string storage = "storage";
                var sequence = new List<string>();
                _validationEntitiesContext
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
                var endCertificates = _validationEntitiesContext.Object.EndCertificates.ToList();
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
                _validationEntitiesContext.Invocations.Clear();

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                VerifyExtractedInformation(Leaf1Certificates, Leaf1TimestampValue, PackageSignatureType.Author);
                Assert.Equal(2, _validationEntitiesContext.Object.EndCertificates.Count());
                Assert.Equal(4, _validationEntitiesContext.Object.ParentCertificates.Count());
                Assert.Equal(4, _validationEntitiesContext.Object.CertificateChainLinks.Count());
                Assert.Equal(1, _validationEntitiesContext.Object.PackageSignatures.Count());
                Assert.Equal(1, _validationEntitiesContext.Object.TrustedTimestamps.Count());
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
                    Type = PackageSignatureType.Author,
                    TrustedTimestamps = new List<TrustedTimestamp>(),
                };

                _validationEntitiesContext
                    .Setup(x => x.ParentCertificates)
                    .Returns(DbSetMockFactory.Create(existingParentCertificate));
                _validationEntitiesContext
                    .Setup(x => x.EndCertificates)
                    .Returns(DbSetMockFactory.Create(existingEndCertificate));
                _validationEntitiesContext
                    .Setup(x => x.CertificateChainLinks)
                    .Returns(DbSetMockFactory.Create(existingLink));
                _validationEntitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature));

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                VerifyExtractedInformation(Leaf1Certificates, Leaf1TimestampValue, PackageSignatureType.Author);
                Assert.Equal(2, _validationEntitiesContext.Object.EndCertificates.Count());
                Assert.Equal(4, _validationEntitiesContext.Object.ParentCertificates.Count());
                Assert.Equal(4, _validationEntitiesContext.Object.CertificateChainLinks.Count());
                Assert.Equal(1, _validationEntitiesContext.Object.PackageSignatures.Count());
                Assert.Equal(1, _validationEntitiesContext.Object.TrustedTimestamps.Count());
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

                _validationEntitiesContext
                    .Setup(x => x.EndCertificates)
                    .Returns(DbSetMockFactory.Create(existingEndCertificate));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("The use of an end certificate cannot change.", ex.Message);
                _validationEntitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Empty(_savedCertificates);
            }

            [Theory]
            [InlineData(PackageSignatureType.Author, TestResources.SignedPackageLeaf1)]
            [InlineData(PackageSignatureType.Repository, TestResources.RepoSignedPackageLeaf1)]
            public async Task RejectsPackageWithMultipleSignatures(PackageSignatureType type, string resourceName)
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(resourceName);
                var existingPackageSignature1 = new PackageSignature
                {
                    Key = 1,
                    PackageKey = _packageKey,
                    Type = type,
                };

                var existingPackageSignature2 = new PackageSignature
                {
                    Key = 2,
                    PackageKey = _packageKey,
                    Type = type,
                };

                _validationEntitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature1, existingPackageSignature2));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("There should never be more than one package signature per package and signature type.", ex.Message);
                _validationEntitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Empty(_savedCertificates);
            }

            [Fact]
            public async Task RejectsAuthorSignedPackageWithChangedSigningCertificateThumbprint()
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
                    },
                    Type = PackageSignatureType.Author,
                };

                _validationEntitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("The thumbprint of the signature end certificate cannot change.", ex.Message);
                _validationEntitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Empty(_savedCertificates);
            }

            [Fact]
            public async Task AcceptsRepoSignedPackageWithChangedSigningCertificateThumbprint()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.RepoSignedPackageLeaf1);
                var existingTrustedTimestamp = new TrustedTimestamp
                {
                    EndCertificate = new EndCertificate
                    {
                        Thumbprint = "something else B",
                    },
                };
                var existingPackageSignature = new PackageSignature
                {
                    Key = 1,
                    PackageKey = _packageKey,
                    EndCertificate = new EndCertificate
                    {
                        Thumbprint = "something else A",
                    },
                    Type = PackageSignatureType.Repository,
                    TrustedTimestamps = new List<TrustedTimestamp>
                    {
                        existingTrustedTimestamp
                    },
                };

                _validationEntitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature));
                _validationEntitiesContext
                    .Setup(x => x.TrustedTimestamps)
                    .Returns(DbSetMockFactory.Create(existingTrustedTimestamp));

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                var newPackageSignature = Assert.Single(_validationEntitiesContext.Object.PackageSignatures);
                Assert.NotSame(existingPackageSignature, newPackageSignature);
                Assert.Equal(TestResources.Leaf1Thumbprint, newPackageSignature.EndCertificate.Thumbprint);

                var newTrustedTimestamp = Assert.Single(_validationEntitiesContext.Object.TrustedTimestamps);
                Assert.NotSame(existingTrustedTimestamp, newTrustedTimestamp);
                Assert.Equal(TestResources.Leaf1TimestampThumbprint, newTrustedTimestamp.EndCertificate.Thumbprint);
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
                    Type = PackageSignatureType.Author,
                };

                _validationEntitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("There should never be more than one trusted timestamp per package signature.", ex.Message);
                _validationEntitiesContext.Verify(
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
                    Type = PackageSignatureType.Author,
                };

                _validationEntitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("The thumbprint of the timestamp end certificate cannot change.", ex.Message);
                _validationEntitiesContext.Verify(
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
                    Type = PackageSignatureType.Author,
                };

                _validationEntitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create(existingPackageSignature));

                // Act & Assert
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => _target.ExtractAsync(_packageKey, signature, _token));
                Assert.Equal("The value of the trusted timestamp cannot change.", ex.Message);
                _validationEntitiesContext.Verify(
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
                VerifyExtractedInformation(Leaf1Certificates, Leaf1TimestampValue, PackageSignatureType.Author);
                Assert.Equal(
                    Leaf1Certificates.Certificates.Count + 1,
                    signature.SignedCms.Certificates.Count + signature.Timestamps.Sum(x => x.SignedCms.Certificates.Count));
            }

            [Fact]
            public async Task IfRepositorySignatureExtractionIsDisabled_IgnoresRepositorySignatureOnRepositorySignedPackage()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.RepoSignedPackageLeaf1);

                _validationEntitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create<PackageSignature>());

                _config.CommitRepositorySignatures = false;

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                Assert.Equal(0, _validationEntitiesContext.Object.PackageSignatures.Count());

                // The repository signature's certificate is still stored on blob storage.
                VerifyStoredCertificates(Leaf1Certificates);
            }

            [Fact]
            public async Task IfRepositorySignatureExtractionIsDisabled_IgnoresRepositorySignatureOnRepositoryCounterSignedPackage()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.AuthorAndRepoSignedPackageLeaf1);

                _validationEntitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(DbSetMockFactory.Create<PackageSignature>());

                _config.CommitRepositorySignatures = false;

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                Assert.Equal(1, _validationEntitiesContext.Object.PackageSignatures.Count());
                Assert.Equal(PackageSignatureType.Author, _validationEntitiesContext.Object.PackageSignatures.First().Type);

                // The repository signature's certificate is still stored on blob storage.
                VerifyStoredCertificates(AuthorAndRepoSignedCertificates);
            }

            [Fact]
            public async Task DeletesRepositorySignatureIfRepositorySignatureIsStrippedFromAuthorAndRepositorySignedPackage()
            {
                // Arrange
                var signature = await TestResources.LoadPrimarySignatureAsync(TestResources.SignedPackageLeaf1);

                var existingAuthorTrustedTimestamp = new TrustedTimestamp
                {
                    Key = 1,
                    Value = Leaf1TimestampValue,
                    EndCertificate = new EndCertificate
                    {
                        Thumbprint = TestResources.Leaf1TimestampThumbprint,
                    },
                };
                var existingRepositoryTrustedTimestamp = new TrustedTimestamp
                {
                    Key = 2,
                    Value = AuthorAndRepoSignedCounterTimestampValue,
                    EndCertificate = new EndCertificate
                    {
                        Thumbprint = AuthorAndRepoSignedCertificates.Countersignature.TimestampEndCertificate.Thumbprint,
                    },
                };
                var existingPackageAuthorSignature = new PackageSignature
                {
                    Key = 1,
                    PackageKey = _packageKey,
                    EndCertificate = new EndCertificate
                    {
                        Thumbprint = AuthorAndRepoSignedCertificates.PrimarySignature.SignatureEndCertificate.Thumbprint,
                    },
                    Type = PackageSignatureType.Author,
                    TrustedTimestamps = new List<TrustedTimestamp>
                    {
                        existingAuthorTrustedTimestamp
                    },
                };
                var existingPackageRepositorySignature = new PackageSignature
                {
                    Key = 2,
                    PackageKey = _packageKey,
                    EndCertificate = new EndCertificate
                    {
                        Thumbprint = AuthorAndRepoSignedCertificates.Countersignature.SignatureEndCertificate.Thumbprint,
                    },
                    Type = PackageSignatureType.Repository,
                    TrustedTimestamps = new List<TrustedTimestamp>
                    {
                        existingRepositoryTrustedTimestamp
                    },
                };

                var packageSignaturesMock = DbSetMockFactory.CreateMock(existingPackageAuthorSignature, existingPackageRepositorySignature);
                var trustedTimestampsMock = DbSetMockFactory.CreateMock(existingAuthorTrustedTimestamp, existingRepositoryTrustedTimestamp);

                _validationEntitiesContext
                    .Setup(x => x.PackageSignatures)
                    .Returns(packageSignaturesMock.Object);
                _validationEntitiesContext
                    .Setup(x => x.TrustedTimestamps)
                    .Returns(trustedTimestampsMock.Object);

                // Act
                await _target.ExtractAsync(_packageKey, signature, _token);

                // Assert
                packageSignaturesMock.Setup(m => m.Remove(existingPackageRepositorySignature));
            }

            private void AssignIds()
            {
                var endCertificates = _validationEntitiesContext.Object.EndCertificates.AsQueryable().ToList();
                for (var key = 1; key <= endCertificates.Count; key++)
                {
                    endCertificates[key - 1].Key = key;
                    foreach (var link in endCertificates[key - 1].CertificateChainLinks.ToList())
                    {
                        link.EndCertificateKey = key;
                    }
                }

                var parentCertificates = _validationEntitiesContext.Object.ParentCertificates.AsQueryable().ToList();
                for (var key = 1; key <= parentCertificates.Count; key++)
                {
                    parentCertificates[key - 1].Key = key;
                    foreach (var link in parentCertificates[key - 1].CertificateChainLinks.ToList())
                    {
                        link.ParentCertificateKey = key;
                    }
                }

                var packageSignatures = _validationEntitiesContext.Object.PackageSignatures.AsQueryable().ToList();
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

                var trustedTimestamps = _validationEntitiesContext.Object.TrustedTimestamps.AsQueryable().ToList();
                for (var key = 1; key <= trustedTimestamps.Count; key++)
                {
                    var trustedTimestamp = trustedTimestamps[key - 1];
                    trustedTimestamp.EndCertificateKey = trustedTimestamp.EndCertificate.Key;
                }
            }

            private void VerifyExtractedInformation(
                ExtractedCertificatesThumbprints expected,
                DateTime timestampValue,
                PackageSignatureType signatureType)
            {
                // Assert the certificates saved to the store.
                VerifyStoredCertificates(expected);

                // Assert the certificates saved to the database.
                var trustedTimestamp = VerifyPackageSignatureRecord(expected.PrimarySignature, timestampValue, signatureType);

                Assert.Equal(
                    expected
                        .PrimarySignature
                        .SignatureParentCertificates
                        .Select(x => x.Thumbprint)
                        .OrderBy(x => x),
                     trustedTimestamp
                        .PackageSignature
                        .EndCertificate
                        .CertificateChainLinks
                        .Select(x => x.ParentCertificate.Thumbprint)
                        .OrderBy(x => x));

                Assert.Equal(
                    expected
                        .PrimarySignature
                        .TimestampParentCertificates
                        .Select(x => x.Thumbprint)
                        .OrderBy(x => x),
                     trustedTimestamp
                        .EndCertificate
                        .CertificateChainLinks
                        .Select(x => x.ParentCertificate.Thumbprint)
                        .OrderBy(x => x));
            }

            private TrustedTimestamp VerifyPackageSignatureRecord(
                SignatureCertificateThumprints expected,
                DateTime timestampValue,
                PackageSignatureType signatureType)
            {
                var signatureEndCertificate = Assert.Single(_validationEntitiesContext
                                    .Object
                                    .EndCertificates
                                    .Where(x => x.Thumbprint == expected.SignatureEndCertificate.Thumbprint));
                Assert.Equal(EndCertificateUse.CodeSigning, signatureEndCertificate.Use);

                var timestampEndCertificate = Assert.Single(_validationEntitiesContext
                    .Object
                    .EndCertificates
                    .Where(x => x.Thumbprint == expected.TimestampEndCertificate.Thumbprint));
                Assert.Equal(EndCertificateUse.Timestamping, timestampEndCertificate.Use);

                var packageSignature = Assert.Single(_validationEntitiesContext
                    .Object
                    .PackageSignatures
                    .Where(x => x.Type == signatureType));
                Assert.Equal(_packageKey, packageSignature.PackageKey);
                Assert.NotEqual(default(DateTime), packageSignature.CreatedAt);
                Assert.Equal(expected.SignatureEndCertificate.Thumbprint, packageSignature.EndCertificate.Thumbprint);
                Assert.Same(signatureEndCertificate, packageSignature.EndCertificate);
                Assert.NotNull(packageSignature.TrustedTimestamps);

                var trustedTimestamp = Assert.Single(_validationEntitiesContext
                    .Object
                    .TrustedTimestamps
                    .Where(x => x.PackageSignature == packageSignature));
                Assert.Same(trustedTimestamp, packageSignature.TrustedTimestamps.Single());
                Assert.Same(packageSignature, trustedTimestamp.PackageSignature);
                Assert.Equal(expected.TimestampEndCertificate.Thumbprint, trustedTimestamp.EndCertificate.Thumbprint);
                Assert.Same(timestampEndCertificate, trustedTimestamp.EndCertificate);
                Assert.Equal(timestampValue, trustedTimestamp.Value);
                Assert.Equal(TrustedTimestampStatus.Valid, trustedTimestamp.Status);

                _validationEntitiesContext.Verify(x => x.SaveChangesAsync(), Times.Once);

                return trustedTimestamp;
            }

            private void VerifyStoredCertificates(ExtractedCertificatesThumbprints expected)
            {
                Assert.Equal(expected.Certificates.Count, _savedCertificates.Count);
                for (var i = 0; i < _savedCertificates.Count; i++)
                {
                    var subject = _savedCertificates[i].Subject;
                    var thumbprint = _savedCertificates[i].ComputeSHA256Thumbprint();
                    Assert.Equal(expected.Certificates[i].Subject, subject);
                    Assert.Equal(expected.Certificates[i].Thumbprint, thumbprint);
                }
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
            public SignatureCertificateThumprints PrimarySignature { get; set; }
            public SignatureCertificateThumprints Countersignature { get; set; }

            public IReadOnlyList<SubjectAndThumbprint> Certificates
            {
                get
                {
                    var all = Enumerable.Empty<SubjectAndThumbprint>();

                    if (PrimarySignature != null)
                    {
                        all = all
                            .Concat(new[] { PrimarySignature.SignatureEndCertificate })
                            .Concat(PrimarySignature.SignatureParentCertificates)
                            .Concat(new[] { PrimarySignature.TimestampEndCertificate })
                            .Concat(PrimarySignature.TimestampParentCertificates);
                    }

                    if (Countersignature != null)
                    {
                        all = all
                            .Concat(new[] { Countersignature.SignatureEndCertificate })
                            .Concat(Countersignature.SignatureParentCertificates)
                            .Concat(new[] { Countersignature.TimestampEndCertificate })
                            .Concat(Countersignature.TimestampParentCertificates);
                    }

                    var thumbprints = new HashSet<string>();
                    var output = new List<SubjectAndThumbprint>();
                    foreach (var certificate in all)
                    {
                        if (thumbprints.Add(certificate.Thumbprint))
                        {
                            output.Add(certificate);
                        }
                    }

                    return output;
                }
            }
        }

        private class SignatureCertificateThumprints
        {
            public IReadOnlyList<SubjectAndThumbprint> SignatureParentCertificates { get; set; }
            public SubjectAndThumbprint SignatureEndCertificate { get; set; }
            public IReadOnlyList<SubjectAndThumbprint> TimestampParentCertificates { get; set; }
            public SubjectAndThumbprint TimestampEndCertificate { get; set; }
        }
    }
}
