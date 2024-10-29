// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.ProcessSignature;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Jobs.Validation.PackageSigning.Telemetry;
using NuGet.Jobs.Validation.Storage;
using NuGet.Packaging.Signing;
using NuGet.Services.Logging;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGetGallery;
using Test.Utility.Signing;
using Tests.ContextHelpers;
using TestUtil;
using Validation.PackageSigning.Core.Tests.Support;
using Xunit;
using Xunit.Abstractions;
using NuGetHashAlgorithmName = NuGet.Common.HashAlgorithmName;

namespace Validation.PackageSigning.ProcessSignature.Tests
{
    [Collection(CertificateIntegrationTestCollection.Name)]
    public class SignatureValidatorIntegrationTests : IDisposable
    {
        // NU3018
        private const string AuthorPrimaryCertificateUntrustedMessage = "The author primary signature's signing certificate is not trusted by the trust provider.";
        private const string AuthorPrimaryCertificateRevocationOfflineMessage = "NU3018: The author primary signature found a chain building issue: " +
            "The revocation function was unable to check revocation because the revocation server could not be reached.";
        private const string AuthorPrimaryCertificateRevocationUnknownMessage = "NU3018: The author primary signature found a chain building issue: " +
            "RevocationStatusUnknown: " +
            "The revocation function was unable to check revocation for the certificate.";
        private const string RepositoryCounterCertificateRevocationOfflineMessage = "NU3018: The repository countersignature found a chain building issue: " +
            "The revocation function was unable to check revocation because the revocation server could not be reached.";
        private const string RepositoryCounterCertificateRevocationUnknownMessage = "NU3018: The repository countersignature found a chain building issue: " +
            "RevocationStatusUnknown: " +
            "The revocation function was unable to check revocation for the certificate.";

        // NU3028
        private const string AuthorPrimaryTimestampCertificateUntrustedMessage = "The author primary signature's timestamping certificate is not trusted by the trust provider.";
        private const string AuthorPrimaryTimestampCertificateRevocationUnknownMessage = "NU3028: The author primary signature's timestamp found a chain building issue: " +
            "RevocationStatusUnknown: " +
            "The revocation function was unable to check revocation for the certificate.";
        private const string AuthorPrimaryTimestampCertificateRevocationOfflineMessage = "NU3028: The author primary signature's timestamp found a chain building issue: " +
            "The revocation function was unable to check revocation because the revocation server could not be reached.";

        private readonly CertificateIntegrationTestFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly PackageSigningStateService _packageSigningStateService;
        private readonly Mock<ICertificateStore> _certificateStore;
        private readonly Mock<IValidationEntitiesContext> _validationEntitiesContext;
        private readonly Mock<IEntitiesContext> _galleryEntitiesContext;
        private readonly SignaturePartsExtractor _signaturePartsExtractor;
        private readonly Mock<IProcessorPackageFileService> _packageFileService;
        private readonly Uri _nupkgUri;

        private readonly Mock<ICorePackageService> _corePackageService;
        private readonly ProcessSignatureConfiguration _configuration;
        private readonly Mock<IOptionsSnapshot<ProcessSignatureConfiguration>> _optionsSnapshot;
        private readonly SignatureFormatValidator _formatValidator;

        private readonly Mock<ITelemetryClient> _telemetryClient;
        private readonly TelemetryService _telemetryService;
        private readonly RecordingLogger<SignatureValidator> _logger;
        private readonly int _packageKey;
        private MemoryStream _packageStream;
        private byte[] _savedPackageBytes;
        private readonly CancellationToken _token;
        private readonly SignatureValidator _target;

        private readonly Mock<IOptionsSnapshot<SasDefinitionConfiguration>> _sasDefinitionConfigurationMock;
        private SasDefinitionConfiguration _sasDefinitionConfiguration;

        private SignatureValidationMessage _message;
        private readonly SignatureValidationMessage _unsignedPackageMessage;
        private readonly SignatureValidationMessage _signedPackage1Message;

        public SignatureValidatorIntegrationTests(CertificateIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _output = output ?? throw new ArgumentNullException(nameof(output));

            _validationEntitiesContext = new Mock<IValidationEntitiesContext>();
            _validationEntitiesContext.Mock();

            _galleryEntitiesContext = new Mock<IEntitiesContext>();
            _galleryEntitiesContext.Mock();

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddXunit(output);

            _packageSigningStateService = new PackageSigningStateService(
                _validationEntitiesContext.Object,
                loggerFactory.CreateLogger<PackageSigningStateService>());

            _certificateStore = new Mock<ICertificateStore>();

            // These dependencies are concrete.
            _configuration = new ProcessSignatureConfiguration
            {
                AllowedRepositorySigningCertificates = new List<string> { "fake-thumbprint" },
                V3ServiceIndexUrl = TestResources.V3ServiceIndexUrl,
                CommitRepositorySignatures = true,
            };
            _optionsSnapshot = new Mock<IOptionsSnapshot<ProcessSignatureConfiguration>>();
            _optionsSnapshot.Setup(x => x.Value).Returns(() => _configuration);
            _formatValidator = new SignatureFormatValidator(_optionsSnapshot.Object);

            _signaturePartsExtractor = new SignaturePartsExtractor(
                _certificateStore.Object,
                _validationEntitiesContext.Object,
                _galleryEntitiesContext.Object,
                _optionsSnapshot.Object,
                loggerFactory.CreateLogger<SignaturePartsExtractor>());

            _packageFileService = new Mock<IProcessorPackageFileService>();
            _nupkgUri = new Uri("https://example-storage/TestProcessor/b777135f-1aac-4ec2-a3eb-1f64fe1880d5/nuget.versioning.4.3.0.nupkg");
            _packageFileService
                .Setup(x => x.GetReadAndDeleteUriAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(() => _nupkgUri);
            _packageFileService
                .Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Stream>()))
                .Returns(Task.CompletedTask)
                .Callback<string, string, Guid, Stream>((_, __, ___, s) =>
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        s.Position = 0;
                        s.CopyTo(memoryStream);
                        _savedPackageBytes = memoryStream.ToArray();
                    }
                });

            _corePackageService = new Mock<ICorePackageService>();

            _telemetryClient = new Mock<ITelemetryClient>();
            _telemetryService = new TelemetryService(_telemetryClient.Object);

            _logger = new RecordingLogger<SignatureValidator>(loggerFactory.CreateLogger<SignatureValidator>());

            _sasDefinitionConfigurationMock = new Mock<IOptionsSnapshot<SasDefinitionConfiguration>>();
            _sasDefinitionConfiguration = new SasDefinitionConfiguration();

            _sasDefinitionConfigurationMock.Setup(a => a.Value).Returns(_sasDefinitionConfiguration);

            // Initialize data.
            _packageKey = 23;
            _unsignedPackageMessage = new SignatureValidationMessage(
                TestResources.UnsignedPackageId,
                TestResources.UnsignedPackageVersion,
                new Uri($"https://unit.test/validation/{TestResources.UnsignedPackageId.ToLowerInvariant()}"),
                Guid.NewGuid());
            _signedPackage1Message = new SignatureValidationMessage(
                TestResources.SignedPackageLeafId,
                TestResources.SignedPackageLeaf1Version,
                new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                Guid.NewGuid());
            _message = _signedPackage1Message;
            _token = CancellationToken.None;

            // Initialize the subject of testing.
            _target = new SignatureValidator(
                _packageSigningStateService,
                _formatValidator,
                _signaturePartsExtractor,
                _packageFileService.Object,
                _corePackageService.Object,
                _optionsSnapshot.Object,
                _sasDefinitionConfigurationMock.Object,
                _telemetryService,
                _logger);
        }

        public async Task<MemoryStream> GetAuthorSignedPackageStream1Async()
        {
            TestUtility.RequireSignedPackage(
                _corePackageService,
                _message.PackageId,
                _message.PackageVersion,
                await _fixture.GetSigningCertificateThumbprintAsync());
            return await _fixture.GetAuthorSignedPackageStream1Async(_output);
        }

        [AdminOnlyFact]
        public async Task AcceptsValidSignedPackage()
        {
            // Arrange
            _packageStream = await GetAuthorSignedPackageStream1Async();

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
            Assert.Empty(result.Issues);
        }

        [AdminOnlyFact]
        public async Task AcceptsExpiredAuthorSigningCertificate()
        {
            // Arrange
            using (var certificate = await _fixture.CreateExpiringSigningCertificateAsync())
            {
                _packageStream = await _fixture.AuthorSignPackageStreamAsync(
                    TestResources.GetResourceStream(TestResources.UnsignedPackage),
                    certificate,
                    _output);

                await SignatureTestUtility.WaitForCertificateExpirationAsync(certificate);

                TestUtility.RequireSignedPackage(
                    _corePackageService,
                    TestResources.UnsignedPackageId,
                    TestResources.UnsignedPackageVersion,
                    certificate.ComputeSHA256Thumbprint());
                _message = _unsignedPackageMessage;

                // Act
                var result = await _target.ValidateAsync(
                   _packageKey,
                   _packageStream,
                   _message,
                   _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);
            }
        }

        [Fact(Skip = "Flaky")]
        public async Task RejectsUntrustedSigningCertificate()
        {
            // Arrange
            using (var certificate = await _fixture.CreateUntrustedSigningCertificateAsync())
            {
                using (certificate.Trust())
                {
                    _packageStream = await _fixture.AuthorSignPackageStreamAsync(
                        TestResources.GetResourceStream(TestResources.UnsignedPackage),
                        certificate.Certificate,
                        _output);
                }

                TestUtility.RequireSignedPackage(
                    _corePackageService,
                    TestResources.UnsignedPackageId,
                    TestResources.UnsignedPackageVersion,
                    certificate.Certificate.ComputeSHA256Thumbprint());
                _message = _unsignedPackageMessage;

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                var issue = Assert.Single(result.Issues);
                var clientIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
                Assert.Equal("NU3018", clientIssue.ClientCode);
                Assert.Equal(AuthorPrimaryCertificateUntrustedMessage, clientIssue.ClientMessage);
            }
        }

        [AdminOnlyFact]
        public async Task RejectsUntrustedTimestampingCertificate()
        {
            // Arrange
            using (var timestampService = await _fixture.CreateUntrustedTimestampServiceAsync())
            {
                byte[] packageBytes;
                using (timestampService.Trust())
                {
                    packageBytes = await _fixture.GenerateAuthorSignedPackageBytesAsync(
                        TestResources.SignedPackageLeaf1,
                        await _fixture.GetSigningCertificateAsync(),
                        timestampService.Url,
                        _output);
                }

                TestUtility.RequireSignedPackage(_corePackageService, 
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    await _fixture.GetSigningCertificateThumbprintAsync());

                _packageStream = new MemoryStream(packageBytes);

                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                // Act
                var result = await _target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                var issue = Assert.Single(result.Issues);
                var clientIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
                Assert.Equal("NU3028", clientIssue.ClientCode);
                Assert.Equal(AuthorPrimaryTimestampCertificateUntrustedMessage, clientIssue.ClientMessage);
            }
        }

        [AdminOnlyFact]
        public async Task AcceptsAuthorSignatureWithIncorrectTimestampAuthorityCertificateHash()
        {
            // Arrange
            // Trusted timestamps place the timestamp authority's signing certificate's SHA-1 hash in
            // a "signing-certificate" attribute. We do not verify this SHA-1 hash, therefore, packages
            // with an incorrect hash should pass validation.
            var options = new TimestampServiceOptions
            {
                SigningCertificateUsage = SigningCertificateUsage.V1,
                SigningCertificateV1Hash = new byte[20]
            };

            using (var timestampService = await _fixture.CreateCustomTimestampServiceAsync(options))
            {
                var packageBytes = await _fixture.GenerateAuthorSignedPackageBytesAsync(
                    TestResources.SignedPackageLeaf1,
                    await _fixture.GetSigningCertificateAsync(),
                    timestampService.Url,
                    _output);

                TestUtility.RequireSignedPackage(
                    _corePackageService,
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    await _fixture.GetSigningCertificateThumbprintAsync());

                _packageStream = new MemoryStream(packageBytes);

                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                var result = await _target.ValidateAsync(
                       _packageKey,
                       _packageStream,
                       _message,
                       _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);
            }
        }

        [AdminOnlyFact]
        public async Task AcceptsRepositorySignatureWithIncorrectTimestampAuthorityCertificateHash()
        {
            // Arrange
            // Trusted timestamps place the timestamp authority's signing certificate's SHA-1 hash in
            // a "signing-certificate" attribute. We do not verify this SHA-1 hash, therefore, packages
            // with an incorrect hash should pass validation.
            var options = new TimestampServiceOptions
            {
                SigningCertificateUsage = SigningCertificateUsage.V1,
                SigningCertificateV1Hash = new byte[20]
            };

            using (var timestampService = await _fixture.CreateCustomTimestampServiceAsync(options))
            {
                var certificate = await _fixture.GetSigningCertificateAsync();
                _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                    TestResources.GetResourceStream(TestResources.UnsignedPackage),
                    certificate,
                    timestampService.Url,
                    _output);

                // Initialize the subject of testing.
                TestUtility.RequireUnsignedPackage(
                    _corePackageService,
                    TestResources.UnsignedPackageId,
                    TestResources.UnsignedPackageVersion);

                _message = _unsignedPackageMessage;

                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                // Act
                var result = await target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);
                Assert.Null(result.NupkgUri);
            }
        }

        [AdminOnlyFact]
        public async Task AcceptsRepositoryCounterSignatureWithIncorrectTimestampAuthorityCertificateHash()
        {
            // Arrange
            // Trusted timestamps place the timestamp authority's signing certificate's SHA-1 hash in
            // a "signing-certificate" attribute. We do not verify this SHA-1 hash, therefore, packages
            // with an incorrect hash should pass validation.
            var options = new TimestampServiceOptions
            {
                SigningCertificateUsage = SigningCertificateUsage.V1,
                SigningCertificateV1Hash = new byte[20]
            };

            using (var timestampService = await _fixture.CreateCustomTimestampServiceAsync(options))
            {
                var certificate = await _fixture.GetSigningCertificateAsync();
                _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                    await GetAuthorSignedPackageStream1Async(),
                    certificate,
                    timestampService.Url,
                    _output);

                // Initialize the subject of testing.
                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                // Act
                var result = await target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token);

                // Assert
                VerifyPackageSigningStatus(
                    result,
                    ValidationStatus.Succeeded,
                    PackageSigningStatus.Valid,
                    repositorySigned: true);
            }
        }

        [AdminOnlyFact]
        public async Task AcceptsTrustedTimestampingCertificateWithUnavailableRevocation()
        {
            // Arrange
            using (var timestampService = await _fixture.CreateTimestampServiceWithUnavailableRevocationAsync())
            {
                byte[] packageBytes;
                using (timestampService.RegisterDefaultResponders())
                {
                    packageBytes = await _fixture.GenerateAuthorSignedPackageBytesAsync(
                        TestResources.SignedPackageLeaf1,
                        await _fixture.GetSigningCertificateAsync(),
                        timestampService.Url,
                        _output);
                }

                // Wait for the OCSP response cached by the operating system during signing to get stale.
                await timestampService.WaitForResponseExpirationAsync();

                TestUtility.RequireSignedPackage(_corePackageService,
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    await _fixture.GetSigningCertificateThumbprintAsync());

                _packageStream = new MemoryStream(packageBytes);

                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                SignatureValidatorResult result;
                using (timestampService.RegisterResponders(addOcsp: false))
                {
                    // Act
                    result = await _target.ValidateAsync(
                       _packageKey,
                       _packageStream,
                       _message,
                       _token);
                }

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);

                var allMessages = string.Join(Environment.NewLine, _logger.Messages);
                Assert.Contains(AuthorPrimaryTimestampCertificateRevocationOfflineMessage, allMessages);
                Assert.Contains(AuthorPrimaryTimestampCertificateRevocationUnknownMessage, allMessages);
            }
        }

        [AdminOnlyFact]
        public async Task AcceptsTrustedSigningCertificateWithUnavailableRevocation()
        {
            // Arrange
            using (var certificateWithUnavailableRevocation = await _fixture.CreateSigningCertificateWithUnavailableRevocationAsync())
            {
                using (certificateWithUnavailableRevocation.RespondToRevocations())
                {
                    _packageStream = await _fixture.AuthorSignPackageStreamAsync(
                        TestResources.GetResourceStream(TestResources.UnsignedPackage),
                        certificateWithUnavailableRevocation.Certificate,
                        _output);
                }

                // Wait for the OCSP response cached by the operating system during signing to get stale.
                await certificateWithUnavailableRevocation.WaitForResponseExpirationAsync();

                TestUtility.RequireSignedPackage(
                    _corePackageService,
                    TestResources.UnsignedPackageId,
                    TestResources.UnsignedPackageVersion,
                    certificateWithUnavailableRevocation.Certificate.ComputeSHA256Thumbprint());
                _message = _unsignedPackageMessage;

                // Act
                var result = await _target.ValidateAsync(
                   _packageKey,
                   _packageStream,
                   _message,
                   _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);

                var allMessages = string.Join(Environment.NewLine, _logger.Messages);
                Assert.Contains(AuthorPrimaryCertificateRevocationOfflineMessage, allMessages);
                Assert.Contains(AuthorPrimaryCertificateRevocationUnknownMessage, allMessages);
            }
        }

        [AdminOnlyFact]
        public async Task RejectsPackageWithAddedFile()
        {
            // Arrange
            var packageStream = await GetAuthorSignedPackageStream1Async();

            AddFileToPackageStream(packageStream);

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            VerifyNU3008(result);
        }

        [AdminOnlyFact]
        public async Task RejectsPackageWithModifiedFile()
        {
            // Arrange
            var packageStream = await GetAuthorSignedPackageStream1Async();

            try
            {
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
                using (var entryStream = zipArchive.GetEntry("TestSigned.leaf-1.nuspec").Open())
                {
                    entryStream.Seek(0, SeekOrigin.End);
                    var extraBytes = Encoding.ASCII.GetBytes(Environment.NewLine);
                    await entryStream.WriteAsync(extraBytes, 0, extraBytes.Length);
                }

                _packageStream = packageStream;
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            VerifyNU3008(result);
        }

        [AdminOnlyFact]
        public async Task RejectsInvalidSignedCms()
        {
            // Arrange
            SetSignatureFileContent(
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                Encoding.ASCII.GetBytes("This is not a valid signed CMS."));

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3003", typedIssue.ClientCode);
            Assert.Equal("The package signature is invalid or cannot be verified on this platform.", typedIssue.ClientMessage);
        }

        [AdminOnlyFact]
        public async Task RejectsMultipleSignatures()
        {
            // Arrange
            SetSignatureContent(
                TestResources.SignedPackageLeafId,
                TestResources.SignedPackageLeaf1Version,
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                configuredSignedCms: signedCms =>
                {
                    using (var additionalCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        TestUtility.RequireSignedPackage(_corePackageService, 
                            TestResources.SignedPackageLeafId,
                            TestResources.SignedPackageLeaf1Version,
                            additionalCertificate.ComputeSHA256Thumbprint());
                        signedCms.ComputeSignature(new CmsSigner(additionalCertificate));
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3009", typedIssue.ClientCode);
            Assert.Equal("The package signature file does not contain exactly one primary signature.", typedIssue.ClientMessage);
        }

        [AdminOnlyFact]
        public async Task RejectsAuthorCounterSignatures()
        {
            // Arrange
            var packageStream = await GetAuthorSignedPackageStream1Async();
            ModifySignatureContent(
                packageStream,
                configuredSignedCms: signedCms =>
                {
                    using (var counterCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        TestUtility.RequireSignedPackage(
                            _corePackageService,
                            _message.PackageId,
                            _message.PackageVersion,
                            counterCertificate.ComputeSHA256Thumbprint());

                        var cmsSigner = new CmsSigner(counterCertificate);
                        cmsSigner.SignedAttributes.Add(AttributeUtility.CreateCommitmentTypeIndication(SignatureType.Author));

                        signedCms.SignerInfos[0].ComputeCounterSignature(cmsSigner);
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.AuthorCounterSignaturesNotSupported, issue.IssueCode);
        }

        [AdminOnlyFact]
        public async Task AcceptsAcceptableRepositorySignatures()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                TestResources.GetResourceStream(TestResources.UnsignedPackage),
                certificate,
                _output);

            // Initialize the subject of testing.
            TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
            _message = _unsignedPackageMessage;

            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate });

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
            Assert.Empty(result.Issues);
            Assert.Null(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task AcceptsExpiredRepositorySigningCertificate()
        {
            // Arrange
            using (var certificate = await _fixture.CreateExpiringSigningCertificateAsync())
            {
                _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                    TestResources.GetResourceStream(TestResources.UnsignedPackage),
                    certificate,
                    _output);

                await SignatureTestUtility.WaitForCertificateExpirationAsync(certificate);

                TestUtility.RequireUnsignedPackage(
                    _corePackageService,
                    TestResources.UnsignedPackageId,
                    TestResources.UnsignedPackageVersion);
                _message = _unsignedPackageMessage;

                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                // Act
                var result = await target.ValidateAsync(
                   _packageKey,
                   _packageStream,
                   _message,
                   _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);
                Assert.Null(result.NupkgUri);
            }
        }

        [AdminOnlyFact]
        public async Task StripsRepositorySignatureWithUnallowedSigningCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                TestResources.GetResourceStream(TestResources.UnsignedPackage),
                certificate,
                _output);

            // Initialize the subject of testing.
            TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
            _message = _unsignedPackageMessage;

            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new X509Certificate2[] { });

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
            Assert.Empty(result.Issues);
            Assert.NotNull(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task StripsRepositorySignatureWithUnallowedRepositoryUrl()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                TestResources.GetResourceStream(TestResources.UnsignedPackage),
                certificate,
                _output);

            // Initialize the subject of testing.
            TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
            _message = _unsignedPackageMessage;

            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate },
                v3ServiceIndexUrl: "https://other-service/v3/index.json");

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
            Assert.Empty(result.Issues);
            Assert.NotNull(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task StripsRepositorySignatureWithUntrustedSigningCertificate()
        {
            // Arrange
            var certificate = await _fixture.CreateUntrustedRootSigningCertificateAsync();

            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                TestResources.GetResourceStream(TestResources.UnsignedPackage),
                certificate,
                _output);

            // Initialize the subject of testing.
            TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
            _message = _unsignedPackageMessage;

            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate });

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
            Assert.Empty(result.Issues);
            Assert.NotNull(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task StripsRepositorySignatureWithNoTimestamp()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                TestResources.GetResourceStream(TestResources.UnsignedPackage),
                certificate,
                timestampUri: null,
                output: _output);

            // Initialize the subject of testing.
            TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
            _message = _unsignedPackageMessage;

            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate });

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
            Assert.Empty(result.Issues);
            Assert.NotNull(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task StripsRepositorySignatureWithUntrustedTimestamp()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();

            using (var timestampService = await _fixture.CreateUntrustedTimestampServiceAsync())
            {
                using (timestampService.Trust())
                {
                    _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                        TestResources.GetResourceStream(TestResources.UnsignedPackage),
                        certificate,
                        timestampUri: timestampService.Url,
                        output: _output);
                }

                // Initialize the subject of testing.
                TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
                _message = _unsignedPackageMessage;

                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                // Act
                var result = await target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
                Assert.Empty(result.Issues);
                Assert.NotNull(result.NupkgUri);
            }
        }

        [AdminOnlyFact]
        public async Task WhenRepositorySignatureWithRevokedSigningCertificate_StripsAtIngestion()
        {
            // Arrange
            var revokableCertificate = await _fixture.CreateRevokableSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                TestResources.GetResourceStream(TestResources.UnsignedPackage),
                revokableCertificate.Certificate,
                _output);

            // Initialize the subject of testing.
            TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
            _message = _unsignedPackageMessage;

            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { revokableCertificate.Certificate });

            revokableCertificate.Revoke();

            // Wait one second for the OCSP response cached by the operating system during signing to get stale.
            await revokableCertificate.WaitForResponseExpirationAsync();

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
            Assert.Empty(result.Issues);
            Assert.NotNull(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task WhenRepositorySignatureWithRevokedTimestampingCertificate_StripsAtIngestion()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();

            using (var timestampService = await _fixture.CreateRevokableTimestampServiceAsync())
            {
                _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                    TestResources.GetResourceStream(TestResources.UnsignedPackage),
                    certificate,
                    timestampUri: timestampService.Url,
                    output: _output);

                // Initialize the subject of testing.
                TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
                _message = _unsignedPackageMessage;

                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                timestampService.Revoke();

                // Wait for the OCSP response cached by the operating system during signing to get stale.
                await timestampService.WaitForResponseExpirationAsync();

                // Act
                var result = await target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
                Assert.Empty(result.Issues);
                Assert.NotNull(result.NupkgUri);
            }
        }

        [AdminOnlyFact]
        public async Task WhenRepositorySignatureIsTampered_StripsAtIngestion()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                TestResources.GetResourceStream(TestResources.UnsignedPackage),
                certificate,
                _output);

            AddFileToPackageStream(_packageStream);

            // Initialize the subject of testing.
            TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
            _message = _unsignedPackageMessage;

            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate });

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Unsigned);
            Assert.Empty(result.Issues);
            Assert.NotNull(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task WhenRepositorySignatureWithRevokedSigningCertificate_ThrowsOnRevalidate()
        {
            // Arrange
            var revokableCertificate = await _fixture.CreateRevokableSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                TestResources.GetResourceStream(TestResources.UnsignedPackage),
                revokableCertificate.Certificate,
                _output);

            // Initialize the subject of testing.
            TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
            _message = _unsignedPackageMessage;

            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { revokableCertificate.Certificate });

            revokableCertificate.Revoke();

            _validationEntitiesContext.Object.PackageSigningStates.Add(new PackageSigningState
            {
                PackageKey = _packageKey,
                SigningStatus = PackageSigningStatus.Valid
            });

            // Wait for the OCSP response cached by the operating system during signing to get stale.
            await revokableCertificate.WaitForResponseExpirationAsync();

            // Act
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token));

            Assert.Equal($"Package was repository signed with a signature that fails verification for validation id '{_message.ValidationId}'", e.Message);
        }

        [AdminOnlyFact]
        public async Task WhenRepositorySignatureWithRevokedTimestampingCertificate_ThrowsOnRevalidate()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();

            using (var timestampService = await _fixture.CreateRevokableTimestampServiceAsync())
            {
                _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                    TestResources.GetResourceStream(TestResources.UnsignedPackage),
                    certificate,
                    timestampUri: timestampService.Url,
                    output: _output);

                // Initialize the subject of testing.
                TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
                _message = _unsignedPackageMessage;

                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                _validationEntitiesContext.Object.PackageSigningStates.Add(new PackageSigningState
                {
                    PackageKey = _packageKey,
                    SigningStatus = PackageSigningStatus.Valid
                });

                timestampService.Revoke();

                // Wait for the OCSP response cached by the operating system during signing to get stale.
                await timestampService.WaitForResponseExpirationAsync();

                // Act
                var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    target.ValidateAsync(
                        _packageKey,
                        _packageStream,
                        _message,
                        _token));

                Assert.Equal($"Package was repository signed with a signature that fails verification for validation id '{_message.ValidationId}'", e.Message);
            }
        }

        [AdminOnlyFact]
        public async Task WhenRepositorySignatureIsTampered_ThrowsOnRevalidate()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                TestResources.GetResourceStream(TestResources.UnsignedPackage),
                certificate,
                _output);

            AddFileToPackageStream(_packageStream);

            // Initialize the subject of testing.
            TestUtility.RequireUnsignedPackage(_corePackageService, TestResources.UnsignedPackageId, TestResources.UnsignedPackageVersion);
            _message = _unsignedPackageMessage;

            _validationEntitiesContext.Object.PackageSigningStates.Add(new PackageSigningState
            {
                PackageKey = _packageKey,
                SigningStatus = PackageSigningStatus.Valid
            });

            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate });

            // Act
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token));

            Assert.Equal($"Package was repository signed with a signature that fails verification for validation id '{_message.ValidationId}'", e.Message);
        }

        [AdminOnlyFact]
        public async Task AcceptsAcceptableRepositoryCounterSignatures()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                await GetAuthorSignedPackageStream1Async(),
                certificate,
                _output);

            // Initialize the subject of testing.
            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate });

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(
                result,
                ValidationStatus.Succeeded,
                PackageSigningStatus.Valid,
                repositorySigned: true);

            Assert.Empty(result.Issues);
            Assert.Null(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task AcceptsRepositoryCounterSignatureWithExpiredSigningCertificate()
        {
            // Arrange
            using (var certificate = await _fixture.CreateExpiringSigningCertificateAsync())
            {
                _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                    await GetAuthorSignedPackageStream1Async(),
                    certificate,
                    _output);

                await SignatureTestUtility.WaitForCertificateExpirationAsync(certificate);

                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                // Act
                var result = await target.ValidateAsync(
                   _packageKey,
                   _packageStream,
                   _message,
                   _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);
                Assert.Null(result.NupkgUri);
            }
        }

        [AdminOnlyFact]
        public async Task StripsRepositoryCounterSignatureWithUnallowedSigningCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                await GetAuthorSignedPackageStream1Async(),
                certificate,
                _output);

            // Initialize the subject of testing.
            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new X509Certificate2[0]);

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyRepositoryCounterSignatureWasStripped(result);

            Assert.Empty(result.Issues);
            Assert.NotNull(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task StripsRepositoryCounterSignatureWithUnallowedRepositoryUrl()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                await GetAuthorSignedPackageStream1Async(),
                certificate,
                _output);

            // Initialize the subject of testing.
            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate },
                v3ServiceIndexUrl: "https://other-service/v3/index.json");

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyRepositoryCounterSignatureWasStripped(result);

            Assert.Empty(result.Issues);
            Assert.NotNull(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task StripsRepositoryCounterSignatureWithUntrustedSigningCertificate()
        {
            // Arrange
            var certificate = await _fixture.CreateUntrustedRootSigningCertificateAsync();

            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                await GetAuthorSignedPackageStream1Async(),
                certificate,
                _output);

            // Initialize the subject of testing.
            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate });

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyRepositoryCounterSignatureWasStripped(result);

            Assert.Empty(result.Issues);
            Assert.NotNull(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task StripsRepositoryCounterSignatureWithNoTimestamp()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                await GetAuthorSignedPackageStream1Async(),
                certificate,
                timestampUri: null,
                output: _output);

            // Initialize the subject of testing.
            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate });

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyRepositoryCounterSignatureWasStripped(result);
            Assert.Empty(result.Issues);
            Assert.NotNull(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task StripsRepositoryCounterSignatureWithUntrustedTimestamp()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();

            using (var timestampService = await _fixture.CreateUntrustedTimestampServiceAsync())
            {
                using (timestampService.Trust())
                {
                    _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                        await GetAuthorSignedPackageStream1Async(),
                        certificate,
                        timestampUri: timestampService.Url,
                        output: _output);
                }

                // Initialize the subject of testing.
                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                // Act
                var result = await target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token);

                // Assert
                VerifyRepositoryCounterSignatureWasStripped(result);
                Assert.Empty(result.Issues);
                Assert.NotNull(result.NupkgUri);
            }
        }

        [AdminOnlyFact]
        public async Task WhenRepositoryCounterSignatureWithRevokedSigningCertificate_StripsAtIngestion()
        {
            // Arrange
            var revokableCertificate = await _fixture.CreateRevokableSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                await GetAuthorSignedPackageStream1Async(),
                revokableCertificate.Certificate,
                _output);

            // Initialize the subject of testing.
            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { revokableCertificate.Certificate });

            revokableCertificate.Revoke();

            // Wait for the OCSP response cached by the operating system during signing to get stale.
            await revokableCertificate.WaitForResponseExpirationAsync();

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyRepositoryCounterSignatureWasStripped(result);
            Assert.Empty(result.Issues);
            Assert.NotNull(result.NupkgUri);
        }

        [AdminOnlyFact]
        public async Task WhenRepositoryCounterSignatureWithRevokedTimestampingCertificate_StripsAtIngestion()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();

            using (var timestampService = await _fixture.CreateRevokableTimestampServiceAsync())
            {
                _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                    await GetAuthorSignedPackageStream1Async(),
                    certificate,
                    timestampUri: timestampService.Url,
                    output: _output);

                // Initialize the subject of testing.
                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                timestampService.Revoke();

                // Wait for the OCSP response cached by the operating system during signing to get stale.
                await timestampService.WaitForResponseExpirationAsync();

                // Act
                var result = await target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token);

                // Assert
                VerifyRepositoryCounterSignatureWasStripped(result);
                Assert.Empty(result.Issues);
                Assert.NotNull(result.NupkgUri);
            }
        }

        [AdminOnlyFact]
        public async Task WhenRepositoryCounterSignatureIsTampered_RejectsAtIngestion()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                await GetAuthorSignedPackageStream1Async(),
                certificate,
                _output);

            AddFileToPackageStream(_packageStream);

            // Initialize the subject of testing.
            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate });

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            VerifyNU3008(result);
        }

        [AdminOnlyFact]
        public async Task WhenRepositoryCounterSignatureWithRevokedSigningCertificate_ThrowsOnRevalidate()
        {
            // Arrange
            var revokableCertificate = await _fixture.CreateRevokableSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                await GetAuthorSignedPackageStream1Async(),
                revokableCertificate.Certificate,
                _output);

            // Initialize the subject of testing.
            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { revokableCertificate.Certificate });

            revokableCertificate.Revoke();

            _validationEntitiesContext.Object.PackageSigningStates.Add(new PackageSigningState
            {
                PackageKey = _packageKey,
                SigningStatus = PackageSigningStatus.Valid
            });

            // Wait for the OCSP response cached by the operating system during signing to get stale.
            await revokableCertificate.WaitForResponseExpirationAsync();

            // Act
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token));

            Assert.Equal($"Package was repository signed with a signature that fails verification for validation id '{_message.ValidationId}'", e.Message);
        }

        [AdminOnlyFact]
        public async Task WhenRepositoryCounterSignatureWithRevokedTimestampingCertificate_ThrowsOnRevalidate()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();

            using (var timestampService = await _fixture.CreateRevokableTimestampServiceAsync())
            {
                _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                    await GetAuthorSignedPackageStream1Async(),
                    certificate,
                    timestampUri: timestampService.Url,
                    output: _output);

                // Initialize the subject of testing.
                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                _validationEntitiesContext.Object.PackageSigningStates.Add(new PackageSigningState
                {
                    PackageKey = _packageKey,
                    SigningStatus = PackageSigningStatus.Valid
                });

                timestampService.Revoke();

                // Wait for the OCSP response cached by the operating system during signing to get stale.
                await timestampService.WaitForResponseExpirationAsync();

                // Act
                var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    target.ValidateAsync(
                        _packageKey,
                        _packageStream,
                        _message,
                        _token));

                Assert.Equal($"Package was repository signed with a signature that fails verification for validation id '{_message.ValidationId}'", e.Message);
            }
        }

        [AdminOnlyFact]
        public async Task WhenRepositoryCounterSignatureIsTampered_ThrowsOnRevalidate()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                await GetAuthorSignedPackageStream1Async(),
                certificate,
                _output);

            AddFileToPackageStream(_packageStream);

            _validationEntitiesContext.Object.PackageSigningStates.Add(new PackageSigningState
            {
                PackageKey = _packageKey,
                SigningStatus = PackageSigningStatus.Valid
            });

            // Initialize the subject of testing.
            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate });

            // Act
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token));

            Assert.Equal($"Package was repository signed with a signature that fails verification for validation id '{_message.ValidationId}'", e.Message);
        }

        [AdminOnlyFact]
        public async Task WhenRepositoryCounterSigned_RejectsUntrustedSigningCertificate()
        {
            // Arrange
            var certificate = await _fixture.GetSigningCertificateAsync();
            TestUtility.RequireSignedPackage(
                _corePackageService,
                TestResources.SignedPackageLeafId,
                TestResources.SignedPackageLeaf1Version,
                TestResources.Leaf1Thumbprint);
            _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                certificate,
                _output);

            _message = new SignatureValidationMessage(
                TestResources.SignedPackageLeafId,
                TestResources.SignedPackageLeaf1Version,
                new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                Guid.NewGuid());

            var target = CreateSignatureValidator(
                allowedRepositorySigningCertificates: new[] { certificate });

            // Act
            var result = await target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            var clientIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3018", clientIssue.ClientCode);
            Assert.Equal(AuthorPrimaryCertificateUntrustedMessage, clientIssue.ClientMessage);
        }

        [AdminOnlyFact]
        public async Task WhenRepositoryCounterSigned_RejectsUntrustedTimestampingCertificate()
        {
            // Arrange
            using (var timestampService = await _fixture.CreateUntrustedTimestampServiceAsync())
            {
                var certificate = await _fixture.GetSigningCertificateAsync();
                using (timestampService.Trust())
                {
                    var packageBytes = await _fixture.GenerateAuthorSignedPackageBytesAsync(
                        TestResources.SignedPackageLeaf1,
                        await _fixture.GetSigningCertificateAsync(),
                        timestampService.Url,
                        _output);

                    _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                        new MemoryStream(packageBytes),
                        certificate,
                        _output);
                }

                TestUtility.RequireSignedPackage(_corePackageService,
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    await _fixture.GetSigningCertificateThumbprintAsync());

                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                // Act
                var result = await target.ValidateAsync(
                    _packageKey,
                    _packageStream,
                    _message,
                    _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
                var issue = Assert.Single(result.Issues);
                var clientIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
                Assert.Equal("NU3028", clientIssue.ClientCode);
                Assert.Equal(AuthorPrimaryTimestampCertificateUntrustedMessage, clientIssue.ClientMessage);
            }
        }

        [AdminOnlyFact]
        public async Task WhenRepositoryCounterSigned_AcceptsTrustedTimestampingCertificateWithUnavailableRevocation()
        {
            // Arrange
            using (var timestampService = await _fixture.CreateTimestampServiceWithUnavailableRevocationAsync())
            {
                var certificate = await _fixture.GetSigningCertificateAsync();
                using (timestampService.RegisterDefaultResponders())
                {
                    var packageBytes = await _fixture.GenerateAuthorSignedPackageBytesAsync(
                        TestResources.SignedPackageLeaf1,
                        await _fixture.GetSigningCertificateAsync(),
                        timestampService.Url,
                        _output);

                    _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                        new MemoryStream(packageBytes),
                        certificate,
                        _output);
                }

                // Wait for the OCSP response cached by the operating system during signing to get stale.
                await timestampService.WaitForResponseExpirationAsync();
                
                TestUtility.RequireSignedPackage(_corePackageService,
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    await _fixture.GetSigningCertificateThumbprintAsync());

                _message = new SignatureValidationMessage(
                    TestResources.SignedPackageLeafId,
                    TestResources.SignedPackageLeaf1Version,
                    new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                    Guid.NewGuid());

                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificate });

                SignatureValidatorResult result;
                using (timestampService.RegisterResponders(addOcsp: false))
                {
                    // Act
                    result = await target.ValidateAsync(
                       _packageKey,
                       _packageStream,
                       _message,
                       _token);
                }

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);

                var allMessages = string.Join(Environment.NewLine, _logger.Messages);
                Assert.Contains(AuthorPrimaryTimestampCertificateRevocationOfflineMessage, allMessages);
                Assert.Contains(AuthorPrimaryTimestampCertificateRevocationUnknownMessage, allMessages);
            }
        }

        [AdminOnlyFact]
        public async Task WhenRepositoryCounterSigned_AcceptsTrustedSigningCertificateWithUnavailableRevocation()
        {
            // Arrange
            using (var certificateWithUnavailableRevocation = await _fixture.CreateSigningCertificateWithUnavailableRevocationAsync())
            {
                using (certificateWithUnavailableRevocation.RespondToRevocations())
                {
                    _packageStream = await _fixture.RepositorySignPackageStreamAsync(
                        await GetAuthorSignedPackageStream1Async(),
                        certificateWithUnavailableRevocation.Certificate,
                        _output);
                }

                // Wait for the OCSP response cached by the operating system during signing to get stale.
                await certificateWithUnavailableRevocation.WaitForResponseExpirationAsync();

                var target = CreateSignatureValidator(
                    allowedRepositorySigningCertificates: new[] { certificateWithUnavailableRevocation.Certificate });

                // Act
                var result = await target.ValidateAsync(
                   _packageKey,
                   _packageStream,
                   _message,
                   _token);

                // Assert
                VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid);
                Assert.Empty(result.Issues);

                var allMessages = string.Join(Environment.NewLine, _logger.Messages);
                Assert.Contains(RepositoryCounterCertificateRevocationOfflineMessage, allMessages);
                Assert.Contains(RepositoryCounterCertificateRevocationUnknownMessage, allMessages);
            }
        }

        [AdminOnlyTheory]
        [InlineData(new[] { SignatureType.Author, SignatureType.Repository })]
        [InlineData(new[] { SignatureType.Repository, SignatureType.Author })]
        public async Task RejectsMutuallyExclusiveCounterSignaturesCommitmentTypes(SignatureType[] counterSignatureTypes)
        {
            // Arrange
            var packageStream = await GetAuthorSignedPackageStream1Async();
            ModifySignatureContent(
                packageStream,
                configuredSignedCms: signedCms =>
                {
                    using (var counterCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        TestUtility.RequireSignedPackage(
                            _corePackageService,
                            _message.PackageId,
                            _message.PackageVersion,
                            counterCertificate.ComputeSHA256Thumbprint());

                        var cmsSigner = new CmsSigner(counterCertificate);
                        foreach (var type in counterSignatureTypes)
                        {
                            cmsSigner.SignedAttributes.Add(AttributeUtility.CreateCommitmentTypeIndication(type));
                        }

                        signedCms.SignerInfos[0].ComputeCounterSignature(cmsSigner);
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3000", typedIssue.ClientCode);
            Assert.Equal("The commitment-type-indication attribute contains an invalid combination of values.", typedIssue.ClientMessage);
        }

        [AdminOnlyTheory(Skip = "https://github.com/NuGet/Engineering/issues/5517")]
        [InlineData("MA0GCyqGSIb3DQEJEAYD")] // base64 of ASN.1 encoded "1.2.840.113549.1.9.16.6.3" OID.
        [InlineData(null)] // No commitment type.
        public async Task AllowsNonAuthorAndRepositoryCounterSignatures(string commitmentTypeOidBase64)
        {
            // Arrange
            _message = new SignatureValidationMessage(
                TestResources.SignedPackageLeafId,
                TestResources.SignedPackageLeaf1Version,
                new Uri($"https://unit.test/validation/{TestResources.SignedPackageLeaf1.ToLowerInvariant()}"),
                Guid.NewGuid());
            var packageStream = await GetAuthorSignedPackageStream1Async();
            ModifySignatureContent(
                packageStream,
                configuredSignedCms: signedCms =>
                {
                    using (var counterCertificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
                    {
                        var cmsSigner = new CmsSigner(counterCertificate);

                        if (commitmentTypeOidBase64 != null)
                        {
                            var value = new AsnEncodedData(
                                Oids.CommitmentTypeIndication,
                                Convert.FromBase64String(commitmentTypeOidBase64));

                            var attribute = new CryptographicAttributeObject(
                                new Oid(Oids.CommitmentTypeIndication),
                                new AsnEncodedDataCollection(value));

                            cmsSigner.SignedAttributes.Add(attribute);
                        }

                        signedCms.SignerInfos[0].ComputeCounterSignature(cmsSigner);
                    }
                });

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);

            // This failure type indicates the counter signature validation passed.
            VerifyNU3008(result);
        }

        [AdminOnlyFact]
        public async Task RejectsInvalidSignatureContent()
        {
            // Arrange
            SetSignatureContent(
                TestResources.SignedPackageLeafId,
                TestResources.SignedPackageLeaf1Version,
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                "!!--:::FOO...");

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3000", typedIssue.ClientCode);
            Assert.Equal("The package signature content is invalid.", typedIssue.ClientMessage);
        }

        [AdminOnlyFact]
        public async Task RejectInvalidSignatureContentVersion()
        {
            // Arrange
            SetSignatureContent(
                TestResources.SignedPackageLeafId,
                TestResources.SignedPackageLeaf1Version,
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                "Version:2" + Environment.NewLine + Environment.NewLine + "2.16.840.1.101.3.4.2.1-Hash:hash");

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.OnlySignatureFormatVersion1Supported, issue.IssueCode);
        }

        [AdminOnlyFact]
        public async Task RejectsNonAuthorSignature()
        {
            // Arrange
            var content = new SignatureContent(
                SigningSpecifications.V1,
                NuGetHashAlgorithmName.SHA256,
                hashValue: "hash");
            SetSignatureContent(
                TestResources.SignedPackageLeafId,
                TestResources.SignedPackageLeaf1Version,
                TestResources.GetResourceStream(TestResources.SignedPackageLeaf1),
                content.GetBytes());

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.OnlyAuthorSignaturesSupported, issue.IssueCode);
        }

        [AdminOnlyFact]
        public async Task RejectsZip64Packages()
        {
            // Arrange
            _packageStream = TestResources.GetResourceStream(TestResources.Zip64Package);

            // Act
            var result = await _target.ValidateAsync(
                _packageKey,
                _packageStream,
                _message,
                _token);

            // Assert
            VerifyPackageSigningStatus(result, ValidationStatus.Failed, PackageSigningStatus.Invalid);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.PackageIsZip64, issue.IssueCode);
        }

        private IDisposable TemporarilyTrustUntrustedCertificate(X509Certificate2 certificate)
        {
            return new TrustedTestCert<X509Certificate2>(
                certificate,
                x => x,
                new[] { X509StorePurpose.CodeSigning, X509StorePurpose.Timestamping },
                StoreName.Root,
                StoreLocation.LocalMachine);
        }

        private void SetSignatureFileContent(MemoryStream packageStream, byte[] fileContent)
        {
            try
            {
                using (var zipFile = new ICSharpCode.SharpZipLib.Zip.ZipFile(packageStream))
                {
                    zipFile.IsStreamOwner = false;

                    zipFile.BeginUpdate();
                    zipFile.Delete(SigningSpecifications.V1.SignaturePath);
                    zipFile.CommitUpdate();
                    zipFile.BeginUpdate();
                    zipFile.Add(
                        new StreamDataSource(new MemoryStream(fileContent)),
                        SigningSpecifications.V1.SignaturePath,
                        CompressionMethod.Stored);
                    zipFile.CommitUpdate();
                }

                packageStream.Position = 0;

                _packageStream = packageStream;
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }
        }

        private SignatureValidator CreateSignatureValidator(
            X509Certificate2[] allowedRepositorySigningCertificates,
            string v3ServiceIndexUrl = TestResources.V3ServiceIndexUrl)
        {
            var thumbprints = allowedRepositorySigningCertificates.Select(c => c.ComputeSHA256Thumbprint());

            _configuration.AllowedRepositorySigningCertificates.AddRange(thumbprints);
            _configuration.V3ServiceIndexUrl = v3ServiceIndexUrl;

            var formatValidator = new SignatureFormatValidator(_optionsSnapshot.Object);

            return new SignatureValidator(
                _packageSigningStateService,
                formatValidator,
                _signaturePartsExtractor,
                _packageFileService.Object,
                _corePackageService.Object,
                _optionsSnapshot.Object,
                _sasDefinitionConfigurationMock.Object,
                _telemetryService,
                _logger);
        }

        private void ModifySignatureContent(MemoryStream packageStream, Action<SignedCms> configuredSignedCms = null)
        {
            SignedCms signedCms;
            try
            {
                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true))
                using (var entryStream = zipArchive.GetEntry(".signature.p7s").Open())
                {
                    var signature = PrimarySignature.Load(entryStream);
                    signedCms = signature.SignedCms;
                }
            }
            catch
            {
                packageStream?.Dispose();
                throw;
            }

            configuredSignedCms(signedCms);

            SetSignatureFileContent(packageStream, signedCms.Encode());
        }

        private void SetSignatureContent(
            string packageId,
            string packageVersion,
            MemoryStream packageStream,
            byte[] signatureContent = null,
            Action<SignedCms> configuredSignedCms = null)
        {
            if (signatureContent == null)
            {
                signatureContent = new SignatureContent(
                    SigningSpecifications.V1,
                    NuGetHashAlgorithmName.SHA256,
                    hashValue: "hash").GetBytes();
            }

            using (var certificate = SigningTestUtility.GenerateCertificate(subjectName: null, modifyGenerator: null))
            {
                TestUtility.RequireSignedPackage(_corePackageService, packageId, packageVersion, certificate.ComputeSHA256Thumbprint());

                var contentInfo = new ContentInfo(signatureContent);
                var signedCms = new SignedCms(contentInfo);

                signedCms.ComputeSignature(new CmsSigner(certificate));

                configuredSignedCms?.Invoke(signedCms);

                var fileContent = signedCms.Encode();

                SetSignatureFileContent(packageStream, fileContent);
            }
        }

        private void SetSignatureContent(string packageId, string packageVersion, MemoryStream packageStream, string signatureContent)
        {
            SetSignatureContent(packageId, packageVersion, packageStream, signatureContent: Encoding.UTF8.GetBytes(signatureContent));
        }

        private void AddFileToPackageStream(MemoryStream packageStream)
        {
            using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: true))
            using (var entryStream = zipArchive.CreateEntry("new-file.txt").Open())
            using (var writer = new StreamWriter(entryStream))
            {
                writer.WriteLine("These contents were added after the package was signed.");
            }
        }

        private void VerifyRepositoryCounterSignatureWasStripped(SignatureValidatorResult result)
            => VerifyPackageSigningStatus(result, ValidationStatus.Succeeded, PackageSigningStatus.Valid, repositorySigned: false);

        private void VerifyPackageSigningStatus(
            SignatureValidatorResult result,
            ValidationStatus validationStatus,
            PackageSigningStatus packageSigningStatus,
            bool? repositorySigned = null)
        {
            Assert.Equal(validationStatus, result.State);
            var state = _validationEntitiesContext
                .Object
                .PackageSigningStates
                .Where(x => x.PackageKey == _packageKey)
                .SingleOrDefault();
            Assert.Equal(packageSigningStatus, state.SigningStatus);

            if (repositorySigned.HasValue)
            {
                var hasRepositorySignature = _validationEntitiesContext
                    .Object
                    .PackageSignatures
                    .Where(s => s.PackageKey == _packageKey)
                    .Any(s => s.Type == PackageSignatureType.Repository);

                Assert.Equal(repositorySigned.Value, hasRepositorySignature);
            }
        }

        private static void VerifyNU3008(SignatureValidatorResult result)
        {
            var issue = Assert.Single(result.Issues);
            Assert.Equal(ValidationIssueCode.ClientSigningVerificationFailure, issue.IssueCode);
            var typedIssue = Assert.IsType<ClientSigningVerificationFailure>(issue);
            Assert.Equal("NU3008", typedIssue.ClientCode);
            Assert.Equal("The package integrity check failed. The package has changed since it was signed. Try clearing the local http-cache and run nuget operation again.", typedIssue.ClientMessage);
        }

        public void Dispose()
        {
            _packageStream?.Dispose();
        }

        private class StreamDataSource : IStaticDataSource
        {
            private readonly Stream _stream;

            public StreamDataSource(Stream stream)
            {
                _stream = stream;
            }

            public Stream GetSource()
            {
                return _stream;
            }
        }
    }
}