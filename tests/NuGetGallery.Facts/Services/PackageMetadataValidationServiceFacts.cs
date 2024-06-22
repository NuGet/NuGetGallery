// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Packaging;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class PackageMetadataValidationServiceFacts
    {
        private const string Id = "NuGet.Versioning";
        private const string Version = "3.4.0.0-ALPHA+1";
        public Mock<IPackageService> MockPackageService { get; private set; }

        private static PackageMetadataValidationService CreateService(
            Mock<IPackageService> packageService = null,
            Mock<IAppConfiguration> config = null)
        {
            packageService = packageService ?? new Mock<IPackageService>();

            packageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);
            packageService.Setup(x => x
                .CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<User>(), It.IsAny<bool>()))
                .Returns((PackageArchiveReader packageArchiveReader, PackageStreamMetadata packageStreamMetadata, User owner, User currentUser, bool isVerified) =>
                {
                    var packageMetadata = PackageMetadata.FromNuspecReader(
                        packageArchiveReader.GetNuspecReader(),
                        strict: true);

                    var newPackage = new Package();
                    newPackage.PackageRegistration = new PackageRegistration { Id = packageMetadata.Id, IsVerified = isVerified };
                    newPackage.Version = packageMetadata.Version.ToString();
                    newPackage.SemVerLevelKey = SemVerLevelKey.ForPackage(packageMetadata.Version, packageMetadata.GetDependencyGroups().AsPackageDependencyEnumerable());

                    return Task.FromResult(newPackage);
                });

            config = config ?? new Mock<IAppConfiguration>();
            var diagnosticsService = new Mock<IDiagnosticsService>();
            diagnosticsService
                .Setup(ds => ds.GetSource(It.IsAny<string>()))
                .Returns(Mock.Of<IDiagnosticsSource>());

            var packageMetadataValidationService = new Mock<PackageMetadataValidationService>(
                packageService.Object,
                config.Object,
                 new Mock<ITyposquattingService>().Object,
                Mock.Of<ITelemetryService>(),
                diagnosticsService.Object,
                Mock.Of<IFeatureFlagService>());

            return packageMetadataValidationService.Object;
        }

        public class TheValidateMetadataBeforeUploadAsync : FactsBase
        {
            private Mock<TestPackageReader> _nuGetPackage;
            private PackageRegistration _packageRegistration;
            private User _currentUser;

            public TheValidateMetadataBeforeUploadAsync()
            {
                _nuGetPackage = GeneratePackage(isSigned: true);
                _packageRegistration = _package.PackageRegistration;
                _packageService
                    .Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(() => _packageRegistration);
                _currentUser = new User { Key = 87456, Username = "test-owner" };
            }

            [Fact]
            public async Task WarnsWhenPreviousVersionWasSignedButPushedVersionIsUnsigned()
            {
                _package.NormalizedVersion = "2.0.1";
                _nuGetPackage = GeneratePackage(version: _package.NormalizedVersion, isSigned: false);
                var previous = new Package
                {
                    CertificateKey = 1,
                    NormalizedVersion = "2.0.0-ALPHA",
                    PackageStatusKey = PackageStatus.Available,
                };
                _packageRegistration.Packages.Add(previous);
                _packageRegistration.Packages.Add(_package);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Equal(
                    $"The previous package version '{previous.NormalizedVersion}' is author signed but the uploaded " +
                    "package is unsigned. To avoid this warning, sign the package before uploading.",
                    Assert.Single(result.Warnings).PlainTextMessage);
                _packageService.Verify(
                    x => x.FindPackageRegistrationById(It.IsAny<string>()),
                    Times.Once);
            }

            [Fact]
            public async Task DoesNotWarnWhenPreviousVersionWasSignedButIsNotAvailable()
            {
                _package.NormalizedVersion = "2.0.1";
                _nuGetPackage = GeneratePackage(version: _package.NormalizedVersion, isSigned: false);
                var previous = new Package
                {
                    CertificateKey = 1,
                    NormalizedVersion = "2.0.0-ALPHA",
                    PackageStatusKey = PackageStatus.Validating,
                };
                _packageRegistration.Packages.Add(previous);
                _packageRegistration.Packages.Add(_package);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task AcceptsUnsignedPackageAfterUnsignedPackage()
            {
                _package.NormalizedVersion = "2.0.1";
                _nuGetPackage = GeneratePackage(version: _package.NormalizedVersion, isSigned: false);
                _packageRegistration.Packages.Add(new Package
                {
                    CertificateKey = 1,
                    NormalizedVersion = "2.0.0-ALPHA",
                    PackageStatusKey = PackageStatus.Available,
                });
                _packageRegistration.Packages.Add(_package);
                _packageRegistration.Packages.Add(new Package
                {
                    NormalizedVersion = "2.0.0-BETA",
                    PackageStatusKey = PackageStatus.Available,
                });

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task AcceptsUnsignedPackageBeforeSignedPackage()
            {
                _package.NormalizedVersion = "2.0.1";
                _nuGetPackage = GeneratePackage(version: _package.NormalizedVersion, isSigned: false);
                _packageRegistration.Packages.Add(_package);
                _packageRegistration.Packages.Add(new Package
                {
                    CertificateKey = 1,
                    NormalizedVersion = "3.0.0.0-ALPHA",
                    PackageStatusKey = PackageStatus.Available,
                });

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task AcceptsSignedPackages()
            {
                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
                _packageService.Verify(
                    x => x.FindPackageRegistrationById(It.IsAny<string>()),
                    Times.Never);
            }

            [Fact]
            public async Task AcceptsUnsignedPackagesWithNoPackageRegistration()
            {
                _nuGetPackage = GeneratePackage(isSigned: false);
                _packageRegistration = null;

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
                _packageService.Verify(
                    x => x.FindPackageRegistrationById(It.IsAny<string>()),
                    Times.Once);
            }

            public static IEnumerable<object[]> WarnsOnMalformedRepositoryMetadata_Data = new[]
            {
                new object[] { null, null, null },
                new object[] { "git", null, null },
                new object[] { "git", "http://something", Strings.WarningNotHttpsOrGitRepositoryUrlScheme },
                new object[] { "git", "https://something", null },
                new object[] { "git", "git://something", null },
                new object[] { "something", "git://something", Strings.WarningNotHttpsRepositoryUrlScheme },
                new object[] { "something", "https://something", null }
            };

            [MemberData(nameof(WarnsOnMalformedRepositoryMetadata_Data))]
            [Theory]
            public async Task WarnsOnMalformedRepositoryMetadata(string repositoryType, string repositoryUrl, string expectedWarning)
            {
                // Arrange
                _nuGetPackage = GeneratePackage(
                    repositoryMetadata: new RepositoryMetadata() { Url = repositoryUrl, Type = repositoryType },
                    isSigned: false);
                _packageRegistration = null;

                // Act
                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                // Assert
                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);

                if (expectedWarning == null)
                {
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Single(result.Warnings);
                    Assert.Equal(expectedWarning, result.Warnings.First().PlainTextMessage);
                }
            }

            [Fact]
            public async Task AggregatesWarnings()
            {
                // Arrange
                _package.NormalizedVersion = "2.0.1";
                _nuGetPackage = GeneratePackage(
                    version: _package.NormalizedVersion,
                    repositoryMetadata: new RepositoryMetadata() { Url = "http://bad", Type = null },
                    isSigned: false);

                var previous = new Package
                {
                    CertificateKey = 1,
                    NormalizedVersion = "2.0.0-ALPHA",
                    PackageStatusKey = PackageStatus.Available,
                };
                _packageRegistration.Packages.Add(previous);
                _packageRegistration.Packages.Add(_package);

                // Act
                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Equal(2, result.Warnings.Count());
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WithTooManyPackageEntries_WhenRejectPackagesWithTooManyPackageEntriesIsFalse_AcceptsPackage(bool isSigned)
            {
                var desiredTotalEntryCount = isSigned ? ushort.MaxValue : ushort.MaxValue - 1;

                _nuGetPackage = GeneratePackage(isSigned: isSigned, desiredTotalEntryCount: desiredTotalEntryCount);
                _config
                    .Setup(x => x.RejectPackagesWithTooManyPackageEntries)
                    .Returns(false);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WithTooManyPackageEntries_WhenRejectPackagesWithTooManyPackageEntriesIsTrue_RejectsPackage(bool isSigned)
            {
                var desiredTotalEntryCount = isSigned ? ushort.MaxValue : ushort.MaxValue - 1;

                _nuGetPackage = GeneratePackage(isSigned: isSigned, desiredTotalEntryCount: desiredTotalEntryCount);
                _config
                    .Setup(x => x.RejectPackagesWithTooManyPackageEntries)
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal("The package contains too many files and/or folders.", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WithNotTooManyPackageEntries_WhenRejectPackagesWithTooManyPackageEntriesIsTrue_AcceptsPackage(bool isSigned)
            {
                var desiredTotalEntryCount = (isSigned ? ushort.MaxValue : ushort.MaxValue - 1) - 1;

                _nuGetPackage = GeneratePackage(isSigned: isSigned, desiredTotalEntryCount: desiredTotalEntryCount);
                _config
                    .Setup(x => x.RejectPackagesWithTooManyPackageEntries)
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("duplicatedFile.txt", "duplicatedFile.txt")]
            [InlineData("./temp/duplicatedFile.txt", "./temp/duplicatedFile.txt")]
            [InlineData("./temp/duplicatedFile.txt", "./temp\\duplicatedFile.txt")]
            [InlineData("./temp\\duplicatedFile.txt", "./temp\\duplicatedFile.txt")]
            [InlineData("duplicatedFile.txt", "duplicatedFile.TXT")]
            [InlineData("./duplicatedFile.txt", "duplicatedFile.txt")]
            [InlineData("./duplicatedFile.txt", "/duplicatedFile.txt")]
            [InlineData("/duplicatedFile.txt", "duplicatedFile.txt")]
            [InlineData(".\\duplicatedFile.txt", "./duplicatedFile.txt")]
            public async Task WithDuplicatedEntries_ReturnsInvalidPackage(params string[] entryNames)
            {
                // Arrange
                _nuGetPackage = GeneratePackage(entryNames: entryNames);

                // Act
                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                // Assert
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal("The package contains one or more duplicated files in the same folder.", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("noDuplicatedFile.txt", "./temp/noDuplicatedFile.txt")]
            [InlineData("./temp1/noDuplicatedFile.txt", "./temp2/noDuplicatedFile.txt")]
            [InlineData("./temp1/noDuplicatedFile.txt", "./temp1/noDuplicatedFile.css")]
            [InlineData("./temp1/noDuplicatedFile.txt", "./temp1\\noDuplicatedFile.css")]
            public async Task WithNoDuplicatedEntries_ReturnsAcceptedPackage(params string[] entryNames)
            {
                // Arrange
                _nuGetPackage = GeneratePackage(entryNames: entryNames);

                // Act
                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                // Assert
                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(true, true)]
            public async Task HandlesMissingLicenseAccordingToSettings(bool allowLicenselessPackages, bool expectedSuccess)
            {
                _nuGetPackage = GeneratePackageWithUserContent(licenseUrl: null, licenseExpression: null, licenseFilename: null);
                _config
                    .Setup(x => x.AllowLicenselessPackages)
                    .Returns(allowLicenselessPackages);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                if (!expectedSuccess)
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.StartsWith("The package has no license information specified.", result.Message.PlainTextMessage);
                    Assert.IsType<LicenseUrlDeprecationValidationMessage>(result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Single(result.Warnings);
                    Assert.IsType<MissingLicenseValidationMessage>(result.Warnings[0]);
                    Assert.StartsWith("All published packages should have license information specified.", result.Warnings[0].PlainTextMessage);
                }
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(true, true)]
            public async Task HandlesMissingLicenseAccordingToSettingsWhenDisplayUploadWarningV2Enabled(bool allowLicenselessPackages, bool expectedSuccess)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseUrl: null, 
                    licenseExpression: null, 
                    licenseFilename: null, 
                    readmeFilename:"readme.md",
                    readmeFileContents: "read me");

                _config
                    .Setup(x => x.AllowLicenselessPackages)
                    .Returns(allowLicenselessPackages);
                _featureFlagService
                    .Setup(ffs => ffs.IsDisplayUploadWarningV2Enabled(_currentUser))
                    .Returns(true);
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedReadmesEnabled(It.IsAny<User>()))
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                if (!expectedSuccess)
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.StartsWith("The package has no license information specified.", result.Message.PlainTextMessage);
                    Assert.IsType<LicenseUrlDeprecationValidationMessage>(result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Single(result.Warnings);
                    Assert.IsType<MissingLicenseValidationMessageV2>(result.Warnings[0]);
                    Assert.StartsWith("License missing. See how to include a license within the package: https://aka.ms/nuget/authoring-best-practices#licensing.", result.Warnings[0].PlainTextMessage);
                }
            }

            const string LicenseDeprecationUrl = "https://aka.ms/deprecateLicenseUrl";
            const string RegularLicenseUrl = "https://example.com/license";

            [Theory]
            [InlineData(null)]
            [InlineData(LicenseDeprecationUrl)]
            [InlineData(RegularLicenseUrl)]
            public async Task RejectsPackageWithBothLicenseExpressionAndFile(string licenseUrl)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseExpression: "MIT",
                    licenseFilename: "license.txt",
                    licenseUrl: licenseUrl == null ? null : new Uri(licenseUrl));

                var ex = await Assert.ThrowsAsync<PackagingException>(() => _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser));

                Assert.Contains("duplicate", ex.Message);
                Assert.Contains("license", ex.Message);
            }

            [Theory]
            [InlineData(false, true)]
            [InlineData(true, false)]
            public async Task HandlesLegacyLicenseUrlPackageAccordingToSettings(bool blockLegacyLicenseUrl, bool expectedSuccess)
            {
                _nuGetPackage = GeneratePackageWithUserContent(licenseUrl: new Uri(RegularLicenseUrl), licenseExpression: null, licenseFilename: null);
                _config
                    .Setup(x => x.BlockLegacyLicenseUrl)
                    .Returns(blockLegacyLicenseUrl);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                if (!expectedSuccess)
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.StartsWith("Specifying <licenseUrl> in the package metadata is no longer allowed, please specify the license in the package.", result.Message.PlainTextMessage);
                    Assert.IsType<LicenseUrlDeprecationValidationMessage>(result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Single(result.Warnings);
                    Assert.StartsWith("The <licenseUrl> element is deprecated. Consider using the <license> element instead.", result.Warnings[0].PlainTextMessage);
                    Assert.IsType<LicenseUrlDeprecationValidationMessage>(result.Warnings[0]);
                }
            }

            [Fact]
            public async Task RejectsLicenseDeprecationUrlWithoutLicense()
            {
                _nuGetPackage = GeneratePackageWithUserContent(licenseUrl: new Uri(LicenseDeprecationUrl), licenseExpression: null, licenseFilename: null);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal("The license deprecation URL must be used in conjunction with specifying the license in the package.", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(null)]
            [InlineData(RegularLicenseUrl)]
            public async Task RejectsAlternativeLicenseUrlForLicenseFiles(string licenseUrl)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseUrl: licenseUrl == null ? null : new Uri(licenseUrl),
                    licenseExpression: null,
                    licenseFilename: "license.txt",
                    licenseFileContents: "Some license text");

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.NotNull(result.Message);
                Assert.StartsWith("To provide a better experience for older clients when a license file is packaged, <licenseUrl> must be set to 'https://aka.ms/deprecateLicenseUrl'.", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("Apache-1.0+ OR MIT", "Apache-1.0%2B+OR+MIT")]
            [InlineData("Apache-1.0+ AND MIT", "Apache-1.0%2B+AND+MIT")]
            [InlineData("Apache-1.0+ AND MIT WITH Classpath-exception-2.0", "Apache-1.0%2B+AND+MIT+WITH+Classpath-exception-2.0")]
            [InlineData("MIT WITH Classpath-exception-2.0", "MIT+WITH+Classpath-exception-2.0")]
            [InlineData("MIT", "Apache-1.0%2B+OR+MIT")]
            public async Task RejectsAlternativeLicenseUrlForLicenseExpressions(string licenseExpression, string licenseUrlPostfix)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseUrl: new Uri($"https://licenses.nuget.org/{licenseUrlPostfix}"),
                    licenseExpression: licenseExpression,
                    licenseFilename: null,
                    licenseFileContents: null);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("malformed license URL", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(null)]
            [InlineData(RegularLicenseUrl)]
            public async Task ErrorsWhenInvalidLicenseUrlSpecifiedWithLicenseExpression(string licenseUrl)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseUrl: licenseUrl == null ? null : new Uri(licenseUrl),
                    licenseExpression: "MIT",
                    licenseFilename: null);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.IsType<InvalidLicenseUrlValidationMessage>(result.Message);
                Assert.StartsWith("To provide a better experience for older clients when a license expression is specified, <licenseUrl> must be set to 'https://licenses.nuget.org/MIT'.", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsUnlicensedPackages()
            {
                _nuGetPackage = GeneratePackageWithUserContent(licenseUrl: new Uri(LicenseDeprecationUrl), licenseExpression: "UNLICENSED", licenseFilename: null);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("UNLICENSED", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("MIT", true)]
            [InlineData("MIT AND MIT", true)]
            [InlineData("(((MIT)))", true)]
            [InlineData("MIT OR GPL-2.0-only", true)]
            [InlineData("MIT or GPL-2.0-only", false)]
            [InlineData("MIT Or GPL-2.0-only", false)]
            [InlineData("(MIT OR GPL-2.0-only)", true)]
            [InlineData("(MIT AND GPL-2.0-only)", true)]
            [InlineData("(MIT and GPL-2.0-only)", false)]
            [InlineData("(MIT And GPL-2.0-only)", false)]
            [InlineData("((((MIT) OR (GPL-2.0-only))))", true)]
            [InlineData("(MIT", false)]
            [InlineData("EUPL-1.1+", true)]
            [InlineData("Vim WITH Font-exception-2.0", true)] // we are not checking if license expression make sense
            [InlineData("Vim with Font-exception-2.0", false)]
            [InlineData("Vim With Font-exception-2.0", false)]
            [InlineData("(EUPL-1.1+ OR (SPL-1.0 WITH Font-exception-2.0) AND Sleepycat)", true)]
            public async Task ChecksLicenseExpressionCorrectness(string licenseExpression, bool expectedSuccess)
            {
                _nuGetPackage = GeneratePackageWithUserContent(licenseUrl: GetLicenseExpressionDeprecationUrl(licenseExpression), licenseExpression: licenseExpression, licenseFilename: null);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                if (!expectedSuccess)
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains("Invalid license metadata", result.Message.PlainTextMessage);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Empty(result.Warnings);
                }
            }

            [Theory]
            [InlineData("MIIT")]
            [InlineData("Mit")]
            [InlineData("mit")]
            public async Task RejectsUnknownLicense(string licenseExpression)
            {
                _nuGetPackage = GeneratePackageWithUserContent(licenseUrl: new Uri(LicenseDeprecationUrl), licenseExpression: licenseExpression, licenseFilename: null);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("Invalid license metadata", result.Message.PlainTextMessage);
                Assert.Contains(licenseExpression, result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("GPL-1.0-only", new[] { "GPL-1.0-only" })]
            [InlineData("GPL-1.0-only+", new[] { "GPL-1.0-only" })]
            [InlineData("RSA-MD", new[] { "RSA-MD" })]
            [InlineData("RSA-MD AND MIT+", new[] { "RSA-MD" })]
            [InlineData("MIT OR GPL-1.0-only", new[] { "GPL-1.0-only" })]
            [InlineData("MIT OR GPL-1.0-only WITH Classpath-exception-2.0", new[] { "GPL-1.0-only" })]
            [InlineData("Saxpath OR GPL-1.0-only WITH Classpath-exception-2.0", new[] { "Saxpath", "GPL-1.0-only" })]
            public async Task RejectsNonOsiFsfLicenses(string licenseExpression, string[] unapprovedLicenses)
            {
                var licenseUri = new Uri($"https://licenses.nuget.org/{licenseExpression}");
                _nuGetPackage = GeneratePackageWithUserContent(licenseUrl: licenseUri, licenseExpression: licenseExpression, licenseFilename: null);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("Open Source Initiative", result.Message.PlainTextMessage);
                Assert.Contains("Free Software Foundation", result.Message.PlainTextMessage);
                foreach (var unapproved in unapprovedLicenses)
                {
                    Assert.Contains(unapproved, result.Message.PlainTextMessage);
                }
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("GPL-1.0")]
            [InlineData("GPL-1.0+")]
            public async Task RejectsDeprecatedLicense(string licenseName)
            {
                _nuGetPackage = GeneratePackageWithUserContent(licenseUrl: new Uri(LicenseDeprecationUrl), licenseExpression: licenseName, licenseFilename: null);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("deprecated", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsPackagesWithMissingLicenseFileWhenSpecified()
            {
                const string licenseFileName = "license.txt";

                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseExpression: null,
                    licenseFilename: licenseFileName,
                    licenseFileContents: null);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("license file", result.Message.PlainTextMessage);
                Assert.Contains("does not exist", result.Message.PlainTextMessage);
                Assert.Contains(licenseFileName, result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("", true)]
            [InlineData(".txt", true)]
            [InlineData(".md", true)]
            [InlineData(".Txt", true)]
            [InlineData(".Md", true)]
            [InlineData(".doc", false)]
            [InlineData(".pdf", false)]
            public async Task ChecksLicenseFileExtension(string extension, bool successExpected)
            {
                string licenseFileName = $"sdfzklgj{extension}";

                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseExpression: null,
                    licenseFilename: licenseFileName,
                    licenseFileContents: "license");

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                if (successExpected)
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains("The license file has an invalid extension", result.Message.PlainTextMessage);
                    Assert.Contains("Extension must be either empty or one of the following", result.Message.PlainTextMessage);
                    Assert.Contains(extension, result.Message.PlainTextMessage);
                    Assert.Empty(result.Warnings);
                }
            }

            // any valid UTF-8 encoded file should be accepted
            public static IEnumerable<object[]> RejectsBinaryFiles_Smoke => new object[][]
            {
                new object[] { new byte[] { 0, 1, 2, 3 }, true },
                new object[] { new byte[] { 10, 13, 12 }, false },
                new object[] { Encoding.UTF8.GetBytes("Sample license/readme test"), false},
                new object[] { Encoding.UTF8.GetBytes("тест тест"), false},
            };

            [Theory]
            [MemberData(nameof(RejectsBinaryFiles_Smoke))]
            public async Task RejectsBinaryLicenseFiles(byte[] licenseFileContent, bool expectedFailure)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseExpression: null,
                    licenseFilename: "license.txt",
                    licenseFileBinaryContents: licenseFileContent);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                if (!expectedFailure)
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains("The license file must be plain text using UTF-8 encoding.", result.Message.PlainTextMessage);
                    Assert.Empty(result.Warnings);
                }
            }

            private static Uri GetLicenseExpressionDeprecationUrl(string licenseExpression)
            {
                return new Uri(string.Format("https://licenses.nuget.org/{0}", licenseExpression));
            }

            [Fact]
            public async Task RejectsLongLicenses()
            {
                const int ExpectedMaxLicenseLength = 1024 * 1024;

                var licenseTextBuilder = new StringBuilder(ExpectedMaxLicenseLength + 100);

                while (licenseTextBuilder.Length < ExpectedMaxLicenseLength + 1)
                {
                    licenseTextBuilder.AppendLine("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.");
                }

                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseFilename: "license.txt",
                    licenseFileContents: licenseTextBuilder.ToString());

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("The license file cannot be larger", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("<license><someChildNode /></license>")]
            [InlineData("<license><someChildNode>license.txt</someChildNode></license>")]
            [InlineData("<license type='file'><someChildNode /></license>")]
            [InlineData("<license type='file'><someChildNode>license.txt</someChildNode></license>")]
            [InlineData("<license type='file'>license.<someChildNode>txt</someChildNode></license>")]
            [InlineData("<license type='expression'><someChildNode /></license>")]
            [InlineData("<license type='expression'><M>M</M><I>I</I><T>T</T></license>")]
            [InlineData("<license type='expression'>M<I>I</I>T</license>")]
            public async Task RejectsLicensesWithChildNodes(string licenseNodeText)
            {
                _nuGetPackage = GeneratePackageWithUserContent(getCustomNuspecNodes: () => licenseNodeText);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("license", result.Message.PlainTextMessage);
                Assert.Contains("child", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("", false)]
            [InlineData("1.0.0", true)]
            [InlineData("1.0", false)]
            [InlineData("1", false)]
            [InlineData("2.0.0", false)]
            public async Task RejectsPackagesWithInvalidLicenseVersion(string version, bool expectedSuccess)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"),
                    getCustomNuspecNodes: () => $"<license type='expression' version='{version}'>MIT</license>");

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                if (expectedSuccess)
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains("version", result.Message.PlainTextMessage);
                    Assert.Empty(result.Warnings);
                }
            }

            public static IEnumerable<object[]> LongLicenseNodeValues_Input => new object[][]
            {
                new object[] { $"<license type='file'>{string.Join("", Enumerable.Range(0, 99).Select(_ => "abcde"))}fg.txt</license>" },
                new object[] { $"<license type='expression'>{string.Join(" OR ", Enumerable.Range(0, 71).Select(_ => "MIT"))} OR 0BSD</license>" },
            };

            [Theory]
            [MemberData(nameof(LongLicenseNodeValues_Input))]
            public async Task RejectsLongLicenseNodeValues(string licenseNodeValue)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    getCustomNuspecNodes: () => licenseNodeValue);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal("The license node value must be shorter than 500 characters.", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsNupkgsReportingIncorrectFileLengthForLicenseFile()
            {
                const string licenseFilename = "license.txt";
                const string licenseFileContents = "abcdefghijklnopqrstuvwxyz";

                // Arrange
                var packageStream = GeneratePackageStream(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseFilename: licenseFilename,
                    licenseFileContents: licenseFileContents);

                PatchFileSizeInPackageStream(licenseFilename, licenseFileContents.Length, packageStream);

                _nuGetPackage = PackageServiceUtility.CreateNuGetPackage(packageStream);

                // Act
                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                // Assert
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("corrupt", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsNupkgsReportingIncorrectFileLengthForNuspecFile()
            {
                const string nuspecFilename = PackageId + ".nuspec";
                string nuspecFileContents = null;

                // Arrange
                var packageStream = GeneratePackageStream();

                using (var zipArchive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: true))
                using (var nuspecStream = zipArchive.GetEntry(nuspecFilename).Open())
                using (var reader = new StreamReader(nuspecStream))
                {
                    nuspecFileContents = await reader.ReadToEndAsync();
                }

                PatchFileSizeInPackageStream(nuspecFilename, nuspecFileContents.Length, packageStream);

                _nuGetPackage = PackageServiceUtility.CreateNuGetPackage(packageStream);

                // Act
                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                // Assert
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("corrupt", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            private static void PatchFileSizeInPackageStream(string fileName, long fileLenth, MemoryStream packageStream)
            {
                var buffer = packageStream.GetBuffer();

                var fileNameInBytes = Encoding.ASCII.GetBytes(fileName);

                // the file name should appear twice in the zip stream:
                // 1. where the compressed stream is saved.
                // 2. in the central directory
                // we'll need to patch stream length in both places

                var firstInstanceOffset = FindSequenceIndex(fileNameInBytes, buffer);
                Assert.True(firstInstanceOffset > 0);
                var firstSizeOffset = firstInstanceOffset - 8;
                Assert.True(firstSizeOffset > 0);
                var firstLength = BitConverter.ToInt32(buffer, firstSizeOffset);
                Assert.Equal(fileLenth, firstLength);

                var secondInstanceOffset = FindSequenceIndex(fileNameInBytes, buffer, firstInstanceOffset + fileName.Length);
                Assert.True(secondInstanceOffset > 0);
                var secondSizeOffset = secondInstanceOffset - 22;
                Assert.True(secondSizeOffset > 0);
                var secondLength = BitConverter.ToInt32(buffer, secondSizeOffset);
                Assert.Equal(fileLenth, secondLength);

                // now that we have offsets, we'll just patch them
                buffer[firstSizeOffset] = 1;
                buffer[secondSizeOffset] = 1;
            }

            [Theory]
            [InlineData("<license>foo</license>")]
            [InlineData("<license type='foo'>bar</license>")]
            public async Task RejectsNupkgsWithUnknownLicenseTypes(string licenseNode)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    getCustomNuspecNodes: () => licenseNode);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("Unsupported license type", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("license/something.txt")]
            [InlineData("license\\anotherthing.txt")]
            [InlineData(".\\license\\morething.txt")]
            [InlineData("./license/lessthing.txt")]
            public async Task AcceptsLicenseFileInSubdirectories(string licensePath)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseFilename: licensePath,
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseFileContents: "some license");

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsPackagesWithEmbeddedIcon()
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    iconFilename: "icon.png",
                    iconFileBinaryContents: new byte[] { 1, 2, 3 });

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("icon", result.Message.PlainTextMessage);
            }

            [Fact]
            public async Task AcceptsPackagesWithEmbeddedIconForFlightedUsers()
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    iconFilename: "icon.jpg",
                    iconFileBinaryContents: new byte[] { 0xFF, 0xD8, 0xFF, 0x32 },
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedIconsEnabled(_currentUser))
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task WarnsAboutPackagesWithIconUrlForFlightedUsers()
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    iconUrl: new Uri("https://nuget.test/icon"),
                    iconFilename: null,
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedIconsEnabled(_currentUser))
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                var warning = Assert.Single(result.Warnings);
                Assert.IsType<IconUrlDeprecationValidationMessage>(warning);
                Assert.StartsWith("The <iconUrl> element is deprecated. Consider using the <icon> element instead.", warning.PlainTextMessage);
                Assert.StartsWith("The &lt;iconUrl&gt; element is deprecated. Consider using the &lt;icon&gt; element instead.", warning.RawHtmlMessage);
            }

            [Fact]
            public async Task DoesntWarnAboutPackagesWithNoIconForFlightedUsers()
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    iconUrl: null,
                    iconFilename: null,
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedIconsEnabled(_currentUser))
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task DoesntWarnAboutPackagesWithIconUrl()
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    iconUrl: new Uri("https://nuget.test/icon"),
                    iconFilename: null,
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedIconsEnabled(_currentUser))
                    .Returns(false);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("<icon><something/></icon>")]
            [InlineData("<icon><something>icon.png</something></icon>")]
            public async Task RejectsIconElementWithChildren(string iconElementText)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    getCustomNuspecNodes: () => iconElementText,
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedIconsEnabled(_currentUser))
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("icon", result.Message.PlainTextMessage);
                Assert.Contains("child", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsPackagesWithMissingIconFile()
            {
                const string iconFilename = "somefile.png";
                var result = await ValidatePackageWithIcon(iconFilename, null);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("icon file", result.Message.PlainTextMessage);
                Assert.Contains("does not exist", result.Message.PlainTextMessage);
                Assert.Contains(iconFilename, result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            public static IEnumerable<string[]> LocalIconFilePaths =>
                new[]
                {
                    new [] { Environment.GetEnvironmentVariable("TEMP") + "\\testimage.png" },
                    new [] { (Environment.GetEnvironmentVariable("TEMP") + "\\sneakyicon.png").Replace("\\", "/") },
                };

            [Theory]
            [InlineData("somefile.png")]
            [InlineData("..\\otherfile.png")]
            [InlineData("../otherfile.png")]
            [MemberData(nameof(LocalIconFilePaths))]
            public async Task DoesNotAccessLocalFileSystemForIconFile(string iconFilename)
            {
                using (var file = new FileStream(iconFilename, FileMode.OpenOrCreate, FileAccess.Write))
                using (var bw = new BinaryWriter(file))
                {
                    var data = new byte[] { 0xFF, 0xD8, 0xFF, 0x32 };
                    await file.WriteAsync(data, 0, data.Length);
                }

                try
                {
                    var result = await ValidatePackageWithIcon(iconFilename, null);

                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains("icon file", result.Message.PlainTextMessage);
                    Assert.Contains("does not exist", result.Message.PlainTextMessage);
                    Assert.Contains(iconFilename.Replace("\\", "/"), result.Message.PlainTextMessage);
                    Assert.Empty(result.Warnings);
                }
                finally
                {
                    if (File.Exists(iconFilename))
                    {
                        File.Delete(iconFilename);
                    }
                }
            }

            [Theory]
            [InlineData("icons/main.png")]
            [InlineData("./other/something.jpg")]
            [InlineData("media\\icon.jpeg")]
            [InlineData(".\\data\\awesome.png")]
            public async Task AcceptsPackagesWithIconsInSubdirectories(string iconFilename)
            {
                var result = await ValidatePackageWithIcon(iconFilename, new byte[] { 0xFF, 0xD8, 0xFF, 0x32 });
                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("", false)]
            [InlineData(".", false)]
            [InlineData(".zip", false)]
            [InlineData(".gif", false)]
            [InlineData(".exe", false)]
            [InlineData(".svg", false)]
            [InlineData(".tif", false)]
            [InlineData(".jfif", false)]
            [InlineData(".png", true)]
            [InlineData(".jpg", true)]
            [InlineData(".jpeg", true)]
            [InlineData(".PNG", true)]
            [InlineData(".JPG", true)]
            [InlineData(".JPEG", true)]
            public async Task ChecksIconFileExtension(string extension, bool expectedSuccess)
            {
                string iconFilename = $"someotherfile{extension}";
                var result = await ValidatePackageWithIcon(iconFilename, new byte[] { 0xFF, 0xD8, 0xFF, 0x32 });

                if (expectedSuccess)
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains(extension, result.Message.PlainTextMessage);
                    Assert.Contains("invalid extension", result.Message.PlainTextMessage);
                    Assert.Contains("jpeg", result.Message.PlainTextMessage);
                    Assert.Contains("jpg", result.Message.PlainTextMessage);
                    Assert.Contains("png", result.Message.PlainTextMessage);
                }
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(42, true)]
            [InlineData(1024, true)]
            [InlineData(1024 * 1024, true)]
            [InlineData(1024 * 1024 + 1, false)]
            public async Task CheckIconFileLength(int fileLength, bool expectedSuccess)
            {
                var iconBinaryContent = new byte[fileLength];
                new byte[] { 0xFF, 0xD8, 0xFF, 0x42 }.CopyTo(iconBinaryContent, 0);
                var result = await ValidatePackageWithIcon("icon.png", iconBinaryContent);

                if (expectedSuccess)
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.StartsWith("The icon file cannot be larger than", result.Message.PlainTextMessage);
                }
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsNupkgsReportingIncorrectFileLengthForIconFile()
            {
                const string iconFilename = "icon.png";
                var iconBinaryContent = new byte[547];
                new byte[] { 0xFF, 0xD8, 0xFF, 0x42 }.CopyTo(iconBinaryContent, 0);

                // Arrange
                var packageStream = GeneratePackageStream(
                    iconFilename: iconFilename,
                    iconFileBinaryContents: iconBinaryContent,
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));

                PatchFileSizeInPackageStream(iconFilename, iconBinaryContent.Length, packageStream);

                _nuGetPackage = PackageServiceUtility.CreateNuGetPackage(packageStream);
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedIconsEnabled(_currentUser))
                    .Returns(true);

                // Act
                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                // Assert
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("corrupt", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("icon.png")]
            [InlineData("icon.jpg")]
            public async Task AcceptsRealImages(string resourceFileName)
            {
                var result = await ValidatePackageWithIcon(resourceFileName, TestDataResourceUtility.GetResourceBytes(resourceFileName));
                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Empty(result.Warnings);
            }

            private async Task<PackageValidationResult> ValidatePackageWithIcon(string iconPath, byte[] iconFileData)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    iconFilename: iconPath,
                    iconFileBinaryContents: iconFileData,
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedIconsEnabled(_currentUser))
                    .Returns(true);

                return await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);
            }

            [Fact]
            public async Task RejectsPackagesWithEmbeddedReadme()
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    readmeFilename: "readme.md",
                    readmeFileContents: "some readme md");

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("readme", result.Message.PlainTextMessage);
            }

            [Fact]
            public async Task AcceptsPackagesWithEmbeddedReadmeForFlightedUsers()
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    readmeFilename: "readme.md",
                    readmeFileContents: "some readme md",
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedReadmesEnabled(_currentUser))
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("<readme><someChildNode/></readme>")]
            [InlineData("<readme><someChildNode/> </readme>")]
            [InlineData("<readme><someChildNode>readme.md</someChildNode></readme>")]
            [InlineData("<readme><someChildNode /></readme>")]
            [InlineData("<readme>readme.<someChildNode>md</someChildNode></readme>")]
            [InlineData("<readme><M>M</M><I>I</I><T>T</T></readme>")]
            [InlineData("<readme>M<I>I</I>T</readme>")]
            public async Task RejectsReadmeElementWithChildren(string readmeElement)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    getCustomNuspecNodes: () => readmeElement,
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedReadmesEnabled(_currentUser))
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("readme", result.Message.PlainTextMessage);
                Assert.Contains("child", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsPackagesWithMissingReadmeFile()
            {
                const string readmeFilename = "readme.md";
                var result = await ValidatePackageWithReadme(readmeFilename, null);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("readme", result.Message.PlainTextMessage);
                Assert.Contains("does not exist", result.Message.PlainTextMessage);
                Assert.Contains(readmeFilename, result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task WarnsAboutPackagesWithoutReadmeWhenDisplayUploadWarningV2Enabled()
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));

                _featureFlagService
                    .Setup(ffs => ffs.IsDisplayUploadWarningV2Enabled(_currentUser))
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                var warning = Assert.Single(result.Warnings);
                Assert.IsType<UploadPackageMissingReadme>(warning);
                Assert.StartsWith("Readme missing. Go to https://aka.ms/nuget-include-readme learn How to include a readme file within the package.", warning.PlainTextMessage);
                Assert.StartsWith("<strong>Readme</strong> missing.<a href=\"https://aka.ms/nuget-include-readme\"> See how to include a readme file within the package</a>, or add it as you upload.", warning.RawHtmlMessage);
            }

            [Fact]
            public async Task WarnsAboutPackagesWithoutWhenEmbeddedReadmeNotEnabledAndDisplayUploadWarningV2Enabled()
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));

                _featureFlagService
                    .Setup(ffs => ffs.IsDisplayUploadWarningV2Enabled(_currentUser))
                    .Returns(true);
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedReadmesEnabled(It.IsAny<User>()))
                    .Returns(false);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                var warning = Assert.Single(result.Warnings);
                Assert.IsType<UploadPackageMissingReadme>(warning);
                Assert.StartsWith("Readme missing. Go to https://aka.ms/nuget-include-readme learn How to include a readme file within the package.", warning.PlainTextMessage);
                Assert.StartsWith("<strong>Readme</strong> missing.<a href=\"https://aka.ms/nuget-include-readme\"> See how to include a readme file within the package</a>, or add it as you upload.", warning.RawHtmlMessage);
            }

            private async Task<PackageValidationResult> ValidatePackageWithReadme(string readmePath, byte[] readmeFileData)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    readmeFilename: readmePath,
                    readmeFileBinaryContents: readmeFileData,
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedReadmesEnabled(_currentUser))
                    .Returns(true);

                return await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);
            }

            [Theory]
            [InlineData("", false)]
            [InlineData(".txt", false)]
            [InlineData(".md", true)]
            [InlineData(".Txt", false)]
            [InlineData(".Md", true)]
            [InlineData(".MD", true)]
            [InlineData(".mD", true)]
            [InlineData(".doc", false)]
            [InlineData(".pdf", false)]
            public async Task ChecksReadmeFileExtension(string extension, bool successExpected)
            {
                string readmeFileName = $"readme{extension}";

                var result = await ValidatePackageWithReadme(readmeFileName, new byte[] { 0xFF, 0xD8, 0xFF, 0x32 });

                if (successExpected)
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains(extension, result.Message.PlainTextMessage);
                    Assert.Contains("invalid extension", result.Message.PlainTextMessage);
                    Assert.Contains("md", result.Message.PlainTextMessage);
                }
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(42, true)]
            [InlineData(1024, true)]
            [InlineData(1024 * 1024 - 1, true)]
            [InlineData(1024 * 1024, true)]
            [InlineData(1024 * 1024 + 1, false)]
            public async Task RejectsLongReadme(int fileLength, bool expectedSuccess)
            {

                var readmeText = new String('a', fileLength);

                _nuGetPackage = GeneratePackageWithUserContent(
                    readmeFilename: "readme.md",
                    readmeFileContents: readmeText,
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedReadmesEnabled(_currentUser))
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                if (expectedSuccess)
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains("The readme file cannot be larger", result.Message.PlainTextMessage);
                }
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsNupkgsReportingIncorrectReadmeFileLengthforReadmeFile()
            {
                const string readmeFilename = "readme.md";
                const string readmeFileContents = "readmedocumentation";

                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedReadmesEnabled(_currentUser))
                    .Returns(true);
                // Arrange
                var packageStream = GeneratePackageStream(
                    readmeFilename: readmeFilename,
                    readmeFileContents: readmeFileContents);

                PatchFileSizeInPackageStream(readmeFilename, readmeFileContents.Length, packageStream);

                _nuGetPackage = PackageServiceUtility.CreateNuGetPackage(packageStream);

                // Act
                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                // Assert
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("corrupt", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("readme/readme.md")]
            [InlineData("readme\\anotherReadme.md")]
            [InlineData(".\\readme\\moreReadme.md")]
            [InlineData("./readme/moreReadme.md")]
            public async Task AcceptsReadmeFileInSubdirectories(string readmePath)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    readmeFilename: readmePath,
                    readmeFileContents: "some readme",
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedReadmesEnabled(_currentUser))
                    .Returns(true);
                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [MemberData(nameof(RejectsBinaryFiles_Smoke))]
            public async Task RejectsBinaryReadmeFiles(byte[] readmeFileContent, bool expectedFailure)
            {
                _nuGetPackage = GeneratePackageWithUserContent(
                    readmeFilename: "readme.md",
                    readmeFileBinaryContents: readmeFileContent,
                    licenseExpression: "MIT",
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"));
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedReadmesEnabled(_currentUser))
                    .Returns(true);

                var result = await _target.ValidateMetadataBeforeUploadAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage),
                    _currentUser);

                if (!expectedFailure)
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains("The readme file must be plain text using UTF-8 encoding.", result.Message.PlainTextMessage);
                    Assert.Empty(result.Warnings);
                }
            }

            /// <summary>
            /// A (quite ineffective) method to search for a sequence in an array
            /// </summary>
            /// <param name="searchItem">byte sequence to search for.</param>
            /// <param name="whereToSearch">the array to search in.</param>
            /// <param name="startIndex">Index in the <paramref name="whereToSearch"/> to start searching from.</param>
            /// <returns>Index of the first byte of the sequence. -1 if not found.</returns>
            private static int FindSequenceIndex(byte[] searchItem, byte[] whereToSearch, int startIndex = 0)
            {
                Assert.True(whereToSearch.Length - startIndex >= searchItem.Length);

                for (int start = startIndex; start < whereToSearch.Length - searchItem.Length; ++start)
                {
                    int searchIndex = 0;

                    while (searchIndex < searchItem.Length && searchItem[searchIndex] == whereToSearch[start + searchIndex])
                    {
                        ++searchIndex;
                    }

                    if (searchIndex == searchItem.Length)
                    {
                        return start;
                    }
                }

                return -1;
            }

            private PackageMetadata GetPackageMetadata(Mock<TestPackageReader> mockPackage)
            {
                return PackageMetadata.FromNuspecReader(mockPackage.Object.GetNuspecReader(), strict: true);
            }
        }

        public class TheValidateAfterGeneratePackageMethod : FactsBase
        {
            private readonly User _currentUser;
            private User _owner;
            private Mock<TestPackageReader> _nuGetPackage;
            private bool _isNewPackageRegistration;
            private List<string> _typosquattingCheckCollisionIds;
            public TheValidateAfterGeneratePackageMethod()
            {
                _currentUser = new User
                {
                    Key = 1,
                    UserCertificates = { new UserCertificate() },
                };
                _owner = _currentUser;
                _package.PackageRegistration.Owners = new List<User> { _currentUser };
                _nuGetPackage = GeneratePackage(isSigned: true);
                _config
                    .Setup(x => x.RejectSignedPackagesWithNoRegisteredCertificate)
                    .Returns(true);
                _isNewPackageRegistration = false;
                _typosquattingService
                    .Setup(x => x.IsUploadedPackageIdTyposquatting(It.IsAny<string>(), It.IsAny<User>(), out _typosquattingCheckCollisionIds))
                    .Returns(false);
            }

            [Fact]
            public async Task RejectsSignedPackageIfSigningIsNotAllowed_HasAnySignerAndOneOwner()
            {
                _currentUser.UserCertificates.Clear();
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.IsType<PackageShouldNotBeSignedUserFixableValidationMessage>(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsSignedPackageIfSigningIsNotAllowed_HasAnySignerAndMultipleOwners()
            {
                _currentUser.UserCertificates.Clear();
                _package.PackageRegistration.Owners.Add(new User { Key = _currentUser.Key + 1 });
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.IsType<PackageShouldNotBeSignedUserFixableValidationMessage>(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsSignedPackageIfSigningIsNotAllowed_HasRequiredSignerThatIsCurrentUser()
            {
                _currentUser.UserCertificates.Clear();
                _package.PackageRegistration.RequiredSigners.Add(_currentUser);
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.IsType<PackageShouldNotBeSignedUserFixableValidationMessage>(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsSignedPackageIfSigningIsNotAllowed_HasRequiredSignerThatIsOtherUser()
            {
                var otherUser = new User
                {
                    Key = _currentUser.Key + 1,
                    Username = "Other",
                };
                _currentUser.UserCertificates.Clear();
                _package.PackageRegistration.RequiredSigners.Add(otherUser);
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal(
                    string.Format(Strings.UploadPackage_PackageIsSignedButMissingCertificate_RequiredSigner, otherUser.Username),
                    result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsSignedPackageIfSigningIsNotAllowed_HasAnySignerAndOwnerThatIsOtherUser()
            {
                _owner = new User
                {
                    Key = _currentUser.Key + 1,
                    Username = "OtherA",
                };
                var ownerB = new User
                {
                    Key = _owner.Key + 2,
                    Username = "OtherB",
                };
                _package.PackageRegistration.Owners.Clear();
                _package.PackageRegistration.Owners.Add(_owner);
                _package.PackageRegistration.Owners.Add(ownerB);
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal(
                    string.Format(Strings.UploadPackage_PackageIsSignedButMissingCertificate_RequiredSigner, _owner.Username),
                    result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsSignedPackageIfSigningIsNotAllowed_HasRequiredSignerAndOwnerThatIsOtherUser()
            {
                _owner = new User
                {
                    Key = _currentUser.Key + 1,
                    Username = "OtherA",
                };
                var ownerB = new User
                {
                    Key = _currentUser.Key + 2,
                    Username = "OtherB",
                };
                _package.PackageRegistration.Owners.Clear();
                _package.PackageRegistration.Owners.Add(_owner);
                _package.PackageRegistration.Owners.Add(ownerB);
                _package.PackageRegistration.RequiredSigners.Add(ownerB);
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal(
                    string.Format(Strings.UploadPackage_PackageIsSignedButMissingCertificate_RequiredSigner, ownerB.Username),
                    result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task AllowsSignedPackageOnOwnerWithNoCertificatesIfConfigAllows()
            {
                _currentUser.UserCertificates.Clear();
                _config
                    .Setup(x => x.RejectSignedPackagesWithNoRegisteredCertificate)
                    .Returns(false);
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task AllowsSignedPackageIfSigningIsRequired()
            {
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task RejectsUnsignedPackageIfSigningIsRequiredIrrespectiveOfConfig(bool configFlag)
            {
                _nuGetPackage = GeneratePackage(isSigned: false);
                _config
                    .Setup(x => x.RejectSignedPackagesWithNoRegisteredCertificate)
                    .Returns(configFlag);
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal(Strings.UploadPackage_PackageIsNotSigned, result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task AllowsBothSignedAndUnsignedPackagesIfSigningIsNotRequired(bool isSigned)
            {
                var ownerB = new User
                {
                    Key = _currentUser.Key + 1,
                };
                _package.PackageRegistration.Owners.Add(ownerB);
                _nuGetPackage = GeneratePackage(isSigned: isSigned);
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task AcceptNotTyposquattingNewVersion()
            {
                _isNewPackageRegistration = true;
                _typosquattingService
                    .Setup(x => x.IsUploadedPackageIdTyposquatting(It.IsAny<string>(), It.IsAny<User>(), out _typosquattingCheckCollisionIds))
                    .Returns(false);
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task AcceptIsTyposquattingCheckNotNewVersion()
            {
                _typosquattingService
                    .Setup(x => x.IsUploadedPackageIdTyposquatting(It.IsAny<string>(), It.IsAny<User>(), out _typosquattingCheckCollisionIds))
                    .Returns(true);
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task AcceptNotTyposquattingNotNewVersion()
            {
                _typosquattingService
                    .Setup(x => x.IsUploadedPackageIdTyposquatting(It.IsAny<string>(), It.IsAny<User>(), out _typosquattingCheckCollisionIds))
                    .Returns(false);
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectIsTyposquattingNewVersion()
            {
                _isNewPackageRegistration = true;
                _typosquattingCheckCollisionIds = new List<string> { "typosquatting_package_Id" };
                _typosquattingService
                    .Setup(x => x.IsUploadedPackageIdTyposquatting(It.IsAny<string>(), It.IsAny<User>(), out _typosquattingCheckCollisionIds))
                    .Returns(true);
                var result = await _target.ValidateMetadaAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal(string.Format(Strings.TyposquattingCheckFails, string.Join(",", _typosquattingCheckCollisionIds)), result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }
        }

        public abstract class FactsBase
        {
            protected const string PackageId = "theId";
            protected readonly Mock<IPackageService> _packageService;
            protected readonly Mock<IAppConfiguration> _config;
            protected readonly Mock<ITyposquattingService> _typosquattingService;
            protected readonly Mock<ITelemetryService> _telemetryService;
            protected readonly Mock<IDiagnosticsService> _diagnosticsService;
            protected Package _package;
            protected Stream _packageFile;
            protected ArgumentException _unexpectedException;
            protected FileAlreadyExistsException _conflictException;
            protected readonly CancellationToken _token;
            protected readonly Mock<IFeatureFlagService> _featureFlagService;
            protected readonly PackageMetadataValidationService _target;

            public FactsBase()
            {
                _packageService = new Mock<IPackageService>();
                _config = new Mock<IAppConfiguration>();
                _config
                    .SetupGet(x => x.AllowLicenselessPackages)
                    .Returns(true);
                _telemetryService = new Mock<ITelemetryService>();

                _typosquattingService = new Mock<ITyposquattingService>();

                _package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = Id,
                    },
                    Version = Version,
                };
                _packageFile = Stream.Null;
                _unexpectedException = new ArgumentException("Fail!");
                _conflictException = new FileAlreadyExistsException("Conflict!");
                _token = CancellationToken.None;
                _diagnosticsService = new Mock<IDiagnosticsService>();

                _diagnosticsService
                    .Setup(ds => ds.GetSource(It.IsAny<string>()))
                    .Returns(Mock.Of<IDiagnosticsSource>());

                _featureFlagService = new Mock<IFeatureFlagService>();
                _featureFlagService
                    .Setup(ffs => ffs.AreEmbeddedIconsEnabled(It.IsAny<User>()))
                    .Returns(false);


                _target = new PackageMetadataValidationService(
                    _packageService.Object,
                    _config.Object,
                    _typosquattingService.Object,
                    _telemetryService.Object,
                    _diagnosticsService.Object,
                    _featureFlagService.Object);
            }

            protected static Mock<TestPackageReader> GeneratePackage(
                string version = "1.2.3-alpha.0",
                RepositoryMetadata repositoryMetadata = null,
                bool isSigned = true,
                int? desiredTotalEntryCount = null,
                IReadOnlyList<string> entryNames = null,
                Func<string> getCustomNuspecNodes = null)
                => GeneratePackageWithUserContent(
                    version: version,
                    repositoryMetadata: repositoryMetadata,
                    isSigned: isSigned,
                    desiredTotalEntryCount: desiredTotalEntryCount,
                    getCustomNuspecNodes: getCustomNuspecNodes,
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"),
                    licenseExpression: "MIT",
                    licenseFilename: null,
                    licenseFileContents: null,
                    licenseFileBinaryContents: null,
                    entryNames: entryNames);

            protected static Mock<TestPackageReader> GeneratePackageWithUserContent(
                string version = "1.2.3-alpha.0",
                RepositoryMetadata repositoryMetadata = null,
                bool isSigned = true,
                int? desiredTotalEntryCount = null,
                Func<string> getCustomNuspecNodes = null,
                Uri iconUrl = null,
                Uri licenseUrl = null,
                string licenseExpression = null,
                string licenseFilename = null,
                string licenseFileContents = null,
                byte[] licenseFileBinaryContents = null,
                string iconFilename = null,
                byte[] iconFileBinaryContents = null,
                string readmeFilename = null,
                string readmeFileContents = null,
                byte[] readmeFileBinaryContents = null,
                IReadOnlyList<string> entryNames = null)
            {
                var packageStream = GeneratePackageStream(
                    version: version,
                    repositoryMetadata: repositoryMetadata,
                    isSigned: isSigned,
                    desiredTotalEntryCount: desiredTotalEntryCount,
                    getCustomNuspecNodes: getCustomNuspecNodes,
                    iconUrl: iconUrl,
                    licenseUrl: licenseUrl,
                    licenseExpression: licenseExpression,
                    licenseFilename: licenseFilename,
                    licenseFileContents: licenseFileContents,
                    licenseFileBinaryContents: licenseFileBinaryContents,
                    iconFilename: iconFilename,
                    iconFileBinaryContents: iconFileBinaryContents,
                    readmeFilename: readmeFilename,
                    readmeFileContents: readmeFileContents,
                    readmeFileBinaryContents: readmeFileBinaryContents,
                    entryNames: entryNames);

                return PackageServiceUtility.CreateNuGetPackage(packageStream);
            }

            protected static MemoryStream GeneratePackageStream(
                string version = "1.2.3-alpha.0",
                RepositoryMetadata repositoryMetadata = null,
                bool isSigned = true,
                int? desiredTotalEntryCount = null,
                Func<string> getCustomNuspecNodes = null,
                Uri iconUrl = null,
                Uri licenseUrl = null,
                string licenseExpression = null,
                string licenseFilename = null,
                string licenseFileContents = null,
                byte[] licenseFileBinaryContents = null,
                string iconFilename = null,
                byte[] iconFileBinaryContents = null,
                string readmeFilename = null,
                string readmeFileContents = null,
                byte[] readmeFileBinaryContents = null,
                IReadOnlyList<string> entryNames = null)
            {
                return PackageServiceUtility.CreateNuGetPackageStream(
                    id: PackageId,
                    version: version,
                    repositoryMetadata: repositoryMetadata,
                    isSigned: isSigned,
                    desiredTotalEntryCount: desiredTotalEntryCount,
                    getCustomNuspecNodes: getCustomNuspecNodes,
                    licenseUrl: licenseUrl,
                    iconUrl: iconUrl,
                    licenseExpression: licenseExpression,
                    licenseFilename: licenseFilename,
                    licenseFileContents: GetBinaryLicenseOrReadmeFileContents(licenseFileBinaryContents, licenseFileContents),
                    iconFilename: iconFilename,
                    iconFileBinaryContents: iconFileBinaryContents,
                    readmeFilename: readmeFilename,
                    readmeFileContents: GetBinaryLicenseOrReadmeFileContents(readmeFileBinaryContents, readmeFileContents),
                    entryNames: entryNames);
            }

            private static byte[] GetBinaryLicenseOrReadmeFileContents(byte[] binaryContents, string stringContents)
            {
                if (binaryContents != null)
                {
                    return binaryContents;
                }

                if (stringContents != null)
                {
                    return Encoding.UTF8.GetBytes(stringContents);
                }

                return null;
            }
        }
    }
}