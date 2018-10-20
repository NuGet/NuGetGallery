// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGetGallery.Configuration;
using NuGetGallery.Packaging;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class PackageUploadServiceFacts
    {
        private const string Id = "NuGet.Versioning";
        private const string Version = "3.4.0.0-ALPHA+1";

        public Mock<IPackageService> MockPackageService { get; private set; }

        private static PackageUploadService CreateService(
            Mock<IPackageService> packageService = null,
            Mock<IReservedNamespaceService> reservedNamespaceService = null,
            Mock<IValidationService> validationService = null,
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

            if (reservedNamespaceService == null)
            {
                reservedNamespaceService = new Mock<IReservedNamespaceService>();

                reservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(new ReservedNamespace[0]);
            }

            validationService = validationService ?? new Mock<IValidationService>();
            config = config ?? new Mock<IAppConfiguration>();

            var packageUploadService = new Mock<PackageUploadService>(
                packageService.Object,
                new Mock<IPackageFileService>().Object,
                new Mock<IEntitiesContext>().Object,
                reservedNamespaceService.Object,
                validationService.Object,
                config.Object,
                new Mock<ITyposquattingService>().Object);

            return packageUploadService.Object;
        }

        public class TheGeneratePackageAsyncMethod
        {
            [Fact]
            public async Task WillCallCreatePackageAsyncCorrectly()
            {
                var key = 0;
                var packageService = new Mock<IPackageService>();
                packageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns((PackageRegistration)null);

                var id = "Microsoft.Aspnet.Mvc";
                var packageUploadService = CreateService(packageService);
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: id);
                var owner = new User { Key = key++, Username = "owner" };
                var currentUser = new User { Key = key++, Username = "user" };

                var package = await packageUploadService.GeneratePackageAsync(id, nugetPackage.Object, new PackageStreamMetadata(), owner, currentUser);

                packageService.Verify(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), owner, currentUser, false), Times.Once);
                Assert.False(package.PackageRegistration.IsVerified);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task WillMarkPackageRegistrationVerifiedFlagCorrectly(bool shouldMarkIdVerified)
            {
                var id = "Microsoft.Aspnet.Mvc";
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefixes = new List<string> { "microsoft.", "microsoft.aspnet." };
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var firstUser = testUsers.First();
                var matchingNamepsaces = testNamespaces
                    .Where(rn => prefixes.Any(pr => id.StartsWith(pr, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                prefixes.ForEach(p => {
                    var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(p, StringComparison.OrdinalIgnoreCase));
                    existingNamespace.Owners.Add(firstUser);
                });

                var reservedNamespaceService = new Mock<IReservedNamespaceService>();
                IReadOnlyCollection<ReservedNamespace> userOwnedMatchingNamespaces = matchingNamepsaces;
                reservedNamespaceService.Setup(s => s.ShouldMarkNewPackageIdVerified(It.IsAny<User>(), It.IsAny<string>(), out userOwnedMatchingNamespaces))
                    .Returns(shouldMarkIdVerified);

                reservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(testNamespaces.ToList().AsReadOnly());

                var packageUploadService = CreateService(reservedNamespaceService: reservedNamespaceService);
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: id);

                var package = await packageUploadService.GeneratePackageAsync(id, nugetPackage.Object, new PackageStreamMetadata(), firstUser, firstUser);

                Assert.Equal(shouldMarkIdVerified, package.PackageRegistration.IsVerified);
            }

            [Fact]
            public async Task WillMarkPackageRegistrationNotVerifiedIfIdMatchesNonOwnedSharedNamespace()
            {
                var id = "Microsoft.Aspnet.Mvc";
                var testNamespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
                var prefixes = new List<string> { "microsoft.", "microsoft.aspnet." };
                var testUsers = ReservedNamespaceServiceTestData.GetTestUsers();
                var firstUser = testUsers.First();
                var lastUser = testUsers.Last();
                prefixes.ForEach(p => {
                    var existingNamespace = testNamespaces.FirstOrDefault(rn => rn.Value.Equals(p, StringComparison.OrdinalIgnoreCase));
                    existingNamespace.IsSharedNamespace = true;
                    existingNamespace.Owners.Add(firstUser);
                });

                var reservedNamespaceService = new Mock<IReservedNamespaceService>();
                IReadOnlyCollection<ReservedNamespace> userOwnedMatchingNamespaces = new List<ReservedNamespace>();
                reservedNamespaceService
                    .Setup(s => s.ShouldMarkNewPackageIdVerified(It.IsAny<User>(), It.IsAny<string>(), out userOwnedMatchingNamespaces))
                    .Returns(false);

                reservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(testNamespaces.ToList().AsReadOnly());

                var packageUploadService = CreateService(reservedNamespaceService: reservedNamespaceService);
                var nugetPackage = PackageServiceUtility.CreateNuGetPackage(id: id);

                var package = await packageUploadService.GeneratePackageAsync(id, nugetPackage.Object, new PackageStreamMetadata(), lastUser, lastUser);

                Assert.False(package.PackageRegistration.IsVerified);
            }
        }

        public class TheValidateBeforeGeneratePackageMethod : FactsBase
        {
            private Mock<TestPackageReader> _nuGetPackage;
            private PackageRegistration _packageRegistration;

            public TheValidateBeforeGeneratePackageMethod()
            {
                _nuGetPackage = GeneratePackage(isSigned: true);
                _packageRegistration = _package.PackageRegistration;
                _packageService
                    .Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(() => _packageRegistration);
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

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Equal(
                    $"The previous package version '{previous.NormalizedVersion}' is author signed but the uploaded " +
                    $"package is unsigned. To avoid this warning, sign the package before uploading.",
                    Assert.Single(result.Warnings));
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

                var result = await _target.ValidateBeforeGeneratePackageAsync(_nuGetPackage.Object, GetPackageMetadata(_nuGetPackage));

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

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object, 
                    GetPackageMetadata(_nuGetPackage));

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

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task AcceptsSignedPackages()
            {
                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

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

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
                _packageService.Verify(
                    x => x.FindPackageRegistrationById(It.IsAny<string>()),
                    Times.Once);
            }

            public static IEnumerable<object[]> WarnsOnMalformedRepositoryMetadata_Data = new []
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
                var result = await _target.ValidateBeforeGeneratePackageAsync(_nuGetPackage.Object, GetPackageMetadata(_nuGetPackage));

                // Assert
                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);

                if (expectedWarning == null)
                {
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(1, result.Warnings.Count());
                    Assert.Equal(expectedWarning, result.Warnings.First());
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
                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

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

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

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

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal("The package contains too many files and/or folders.", result.Message);
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

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(false, false)]
            [InlineData(true, true)]
            public async Task HandlesMissingLicenseAccordingToSettings(bool allowLicenselessPackages, bool expectedSuccess)
            {
                _nuGetPackage = GeneratePackage(licenseUrl: null, licenseExpression: null, licenseFilename: null);
                _config
                    .Setup(x => x.AllowLicenselessPackages)
                    .Returns(allowLicenselessPackages);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                if (!expectedSuccess)
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Equal("Package has no license information specified.", result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
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
                _nuGetPackage = GeneratePackage(
                    licenseExpression: "MIT",
                    licenseFilename: "license.txt",
                    licenseUrl: licenseUrl == null ? null : new Uri(licenseUrl));

                var ex = await Assert.ThrowsAsync<PackagingException>(() => _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage)));

                Assert.Contains("duplicate", ex.Message);
                Assert.Contains("license", ex.Message);
            }

            [Fact]
            public async Task RejectsPackageWithLicenseExpression()
            {
                // we don't support license expressions yet. The test to be removed when full support is implemented.

                _nuGetPackage = GeneratePackage(
                    licenseExpression: "MIT",
                    licenseFilename: null,
                    licenseUrl: new Uri(LicenseDeprecationUrl));

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("License expressions are not currenly supported", result.Message);
            }

            [Theory]
            [InlineData(false, true)]
            [InlineData(true, false)]
            public async Task HandlesLegacyLicenseUrlPackageAccordingToSettings(bool blockLegacyLicenseUrl, bool expectedSuccess)
            {
                _nuGetPackage = GeneratePackage(licenseUrl: new Uri(RegularLicenseUrl), licenseExpression: null, licenseFilename: null);
                _config
                    .Setup(x => x.BlockLegacyLicenseUrl)
                    .Returns(blockLegacyLicenseUrl);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                if (!expectedSuccess)
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Equal("Specifying external license URLs are not allowed anymore, please specify the license in the package.", result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Single(result.Warnings);
                    Assert.Equal("Specifying external license URLs is being deprecated, please consider switching to specifying the license in the package.", result.Warnings[0]);
                }
            }

            [Fact]
            public async Task RejectsLicenseDeprecationUrlWithoutLicense()
            {
                _nuGetPackage = GeneratePackage(licenseUrl: new Uri(LicenseDeprecationUrl), licenseExpression: null, licenseFilename: null);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal("The license deprecation URL must be used in conjunction with specifying the license in the package.", result.Message);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(null, "license.txt", null)]
            [InlineData(null, "license.txt", RegularLicenseUrl)]
            public async Task RequiresDeprecationUrlWithEmbeddedLicense(string licenseExpression, string licenseFilename, string licenseUrl)
            {
                _nuGetPackage = GeneratePackage(
                    licenseUrl: licenseUrl == null ? null : new Uri(licenseUrl),
                    licenseExpression: licenseExpression,
                    licenseFilename: licenseFilename,
                    licenseFileContents: licenseFilename);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal("For backwards compatibility when a license is specified in the package, its <licenseUrl> node must point to https://aka.ms/deprecateLicenseUrl", result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsPackagesWithMissingLicenseFileWhenSpecified()
            {
                const string licenseFileName = "license.txt";

                _nuGetPackage = GeneratePackage(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseExpression: null,
                    licenseFilename: licenseFileName,
                    licenseFileContents: null);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("file", result.Message);
                Assert.Contains("does not exist", result.Message);
                Assert.Contains(licenseFileName, result.Message);
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

                _nuGetPackage = GeneratePackage(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseExpression: null,
                    licenseFilename: licenseFileName,
                    licenseFileContents: "license");

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                if (successExpected)
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains("License file has invalid extension", result.Message);
                    Assert.Contains("Extension must be one of the following", result.Message);
                    Assert.Contains(extension, result.Message);
                    Assert.Empty(result.Warnings);
                }
            }

            // any valid UTF-8 encoded file should be accepted
            public static IEnumerable<object[]> RejectsBinaryLicenseFiles_Smoke => new object[][]
            {
                new object[] { new byte[] { 0, 1, 2, 3 }, true },
                new object[] { new byte[] { 10, 13 }, false },
                new object[] { Encoding.UTF8.GetBytes("Sample license test"), false},
                new object[] { Encoding.UTF8.GetBytes("тест тест"), false},
            };

            [Theory]
            [MemberData(nameof(RejectsBinaryLicenseFiles_Smoke))]
            public async Task RejectsBinaryLicenseFiles(byte[] licenseFileContent, bool expectedFailure)
            {
                _nuGetPackage = GeneratePackage(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseExpression: null,
                    licenseFilename: "license.txt",
                    licenseFileBinaryContents: licenseFileContent);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                if (!expectedFailure)
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains("License file must be plain text using UTF-8 encoding.", result.Message);
                    Assert.Empty(result.Warnings);
                }
            }

            private static string[] LicenseNodeVariants => new string[]
            {
                // TODO: uncomment. Client has a bug apparently, which makes it not like the <license> nodes without attributes
                //"<license/>",
                //"<license></license>",
                //"<license> </license>",
                //"<license>ttt</license>",
                "<license type='file'>fff</license>",
                "<license type='expression'>ee</license>",
                "<license type='foobar'>ttt</license>",
            };

            public static IEnumerable<object[]> RejectsLicensedPackagesWhenConfigured_Input =>
                from licenseNode in LicenseNodeVariants
                select new object[] { licenseNode, true, false };

            [Theory]
            [MemberData(nameof(RejectsLicensedPackagesWhenConfigured_Input))]
            public async Task RejectsLicensedPackagesWhenConfigured(string licenseNode, bool rejectPackagesWithLicense, bool expectedSuccess)
            {
                _config
                    .SetupGet(x => x.RejectPackagesWithLicense)
                    .Returns(rejectPackagesWithLicense);
                _nuGetPackage = GeneratePackage(getCustomNuspecNodes: () => licenseNode);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                if (expectedSuccess)
                {
                    Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                    Assert.Null(result.Message);
                    Assert.Empty(result.Warnings);
                }
                else
                {
                    Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                    Assert.Contains("license", result.Message);
                    Assert.Empty(result.Warnings);
                }
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

                _nuGetPackage = GeneratePackage(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseFilename: "license.txt",
                    licenseFileContents: licenseTextBuilder.ToString());

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("License file is too long", result.Message);
                Assert.Empty(result.Warnings);
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);

                Assert.Equal(PackageValidationResultType.PackageShouldNotBeSignedButCanManageCertificates, result.Type);
                Assert.Equal(Strings.UploadPackage_PackageIsSignedButMissingCertificate_CurrentUserCanManageCertificates, result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsSignedPackageIfSigningIsNotAllowed_HasAnySignerAndMultipleOwners()
            {
                _currentUser.UserCertificates.Clear();
                _package.PackageRegistration.Owners.Add(new User { Key = _currentUser.Key + 1 });

                var result = await _target.ValidateAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);

                Assert.Equal(PackageValidationResultType.PackageShouldNotBeSignedButCanManageCertificates, result.Type);
                Assert.Equal(Strings.UploadPackage_PackageIsSignedButMissingCertificate_CurrentUserCanManageCertificates, result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsSignedPackageIfSigningIsNotAllowed_HasRequiredSignerThatIsCurrentUser()
            {
                _currentUser.UserCertificates.Clear();
                _package.PackageRegistration.RequiredSigners.Add(_currentUser);

                var result = await _target.ValidateAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);

                Assert.Equal(PackageValidationResultType.PackageShouldNotBeSignedButCanManageCertificates, result.Type);
                Assert.Equal(Strings.UploadPackage_PackageIsSignedButMissingCertificate_CurrentUserCanManageCertificates, result.Message);
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);

                Assert.Equal(PackageValidationResultType.PackageShouldNotBeSigned, result.Type);
                Assert.Equal(
                    string.Format(Strings.UploadPackage_PackageIsSignedButMissingCertificate_RequiredSigner, otherUser.Username),
                    result.Message);
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);

                Assert.Equal(PackageValidationResultType.PackageShouldNotBeSigned, result.Type);
                Assert.Equal(
                    string.Format(Strings.UploadPackage_PackageIsSignedButMissingCertificate_RequiredSigner, _owner.Username),
                    result.Message);
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);

                Assert.Equal(PackageValidationResultType.PackageShouldNotBeSigned, result.Type);
                Assert.Equal(
                    string.Format(Strings.UploadPackage_PackageIsSignedButMissingCertificate_RequiredSigner, ownerB.Username),
                    result.Message);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task AllowsSignedPackageOnOwnerWithNoCertificatesIfConfigAllows()
            {
                _currentUser.UserCertificates.Clear();
                _config
                    .Setup(x => x.RejectSignedPackagesWithNoRegisteredCertificate)
                    .Returns(false);

                var result = await _target.ValidateAfterGeneratePackageAsync(
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
                var result = await _target.ValidateAfterGeneratePackageAsync(
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal(Strings.UploadPackage_PackageIsNotSigned, result.Message);
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
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
                var result = await _target.ValidateAfterGeneratePackageAsync(
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
                var result = await _target.ValidateAfterGeneratePackageAsync(
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
                var result = await _target.ValidateAfterGeneratePackageAsync(
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
                _typosquattingCheckCollisionIds = new List<string>{ "typosquatting_package_Id" };
                _typosquattingService
                    .Setup(x => x.IsUploadedPackageIdTyposquatting(It.IsAny<string>(), It.IsAny<User>(), out _typosquattingCheckCollisionIds))
                    .Returns(true);

                var result = await _target.ValidateAfterGeneratePackageAsync(
                    _package,
                    _nuGetPackage.Object,
                    _owner,
                    _currentUser,
                    _isNewPackageRegistration);

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal(string.Format(Strings.TyposquattingCheckFails, string.Join(",", _typosquattingCheckCollisionIds)), result.Message);
                Assert.Contains("similar", result.Message);
                Assert.Empty(result.Warnings);
            }
        }

        public class TheCommitPackageMethod : FactsBase
        {
            public static IEnumerable<object[]> SupportedPackageStatuses => new[]
            {
                new object[] { PackageStatus.Available },
                new object[] { PackageStatus.Validating },
            };

            public static IEnumerable<object[]> UnsupportedPackageStatuses => Enum
                .GetValues(typeof(PackageStatus))
                .Cast<PackageStatus>()
                .Concat(new[] { (PackageStatus)(-1) })
                .Where(s => !SupportedPackageStatuses.Any(o => s.Equals(o[0])))
                .Select(s => new object[] { s });

            [Theory]
            [MemberData(nameof(SupportedPackageStatuses))]
            public async Task CommitsAfterSavingSupportedPackageStatuses(PackageStatus packageStatus)
            {
                _package.PackageStatusKey = PackageStatus.FailedValidation;

                _validationService
                    .Setup(vs => vs.StartValidationAsync(_package))
                    .Returns(Task.CompletedTask)
                    .Callback(() => _package.PackageStatusKey = packageStatus);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Once);
                Assert.Equal(PackageCommitResult.Success, result);
            }

            [Theory]
            [MemberData(nameof(UnsupportedPackageStatuses))]
            public async Task RejectsUnsupportedPackageStatuses(PackageStatus packageStatus)
            {
                _package.PackageStatusKey = PackageStatus.Available;

                _validationService
                    .Setup(vs => vs.StartValidationAsync(_package))
                    .Returns(Task.CompletedTask)
                    .Callback(() => _package.PackageStatusKey = packageStatus);

                await Assert.ThrowsAsync<ArgumentException>(
                    () => _target.CommitPackageAsync(_package, _packageFile));

                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
            }

            [Theory]
            [MemberData(nameof(SupportedPackageStatuses))]
            public async Task StartsAsynchronousValidation(PackageStatus packageStatus)
            {
                _package.PackageStatusKey = packageStatus;

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _validationService.Verify(
                    x => x.StartValidationAsync(_package),
                    Times.Once);
                _validationService.Verify(
                    x => x.StartValidationAsync(It.IsAny<Package>()),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(SupportedPackageStatuses))]
            public async Task StartsValidationBeforeOtherPackageOperations(PackageStatus packageStatus)
            {
                _package.PackageStatusKey = packageStatus;

                bool otherOperationsDone = false;
                _validationService
                    .Setup(vs => vs.StartValidationAsync(It.IsAny<Package>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => Assert.False(otherOperationsDone));

                _entitiesContext
                    .Setup(ec => ec.SaveChangesAsync())
                    .Returns(Task.FromResult(1))
                    .Callback(() => otherOperationsDone = true);
                _packageFileService
                    .Setup(pfs => pfs.SaveValidationPackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => otherOperationsDone = true);
                _packageFileService
                    .Setup(x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Returns(Task.CompletedTask)
                    .Callback(() => otherOperationsDone = true);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _validationService
                    .Verify(vs => vs.StartValidationAsync(It.IsAny<Package>()),
                    Times.AtLeastOnce);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Once);
            }

            [Fact]
            public async Task SavesPackageToStorageAndDatabaseWhenAvailable()
            {
                _package.PackageStatusKey = PackageStatus.Available;

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _packageFileService.Verify(
                    x => x.SavePackageFileAsync(_package, _packageFile),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.SaveValidationPackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Never);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Once);
                Assert.Equal(PackageCommitResult.Success, result);
            }

            [Fact]
            public async Task SavesPackageToStorageAndDatabaseWhenValidating()
            {
                _package.PackageStatusKey = PackageStatus.Validating;

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _packageFileService.Verify(
                    x => x.SaveValidationPackageFileAsync(_package, _packageFile),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.SaveValidationPackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Never);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Once);
                Assert.Equal(PackageCommitResult.Success, result);
            }

            [Fact]
            public async Task DoesNotCommitToDatabaseWhenSavingTheFileFails()
            {
                _packageFileService
                    .Setup(x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Throws(_unexpectedException);

                var exception = await Assert.ThrowsAsync(
                    _unexpectedException.GetType(),
                    () => _target.CommitPackageAsync(_package, _packageFile));

                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Same(_unexpectedException, exception);
            }

            [Fact]
            public async Task DoesNotCommitToDatabaseWhenThePackageFileConflicts()
            {
                _packageFileService
                    .Setup(x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Throws(_conflictException);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Equal(PackageCommitResult.Conflict, result);
            }

            [Fact]
            public async Task DoesNotCommitToDatabaseWhenTheValidationFileConflicts()
            {
                _package.PackageStatusKey = PackageStatus.Validating;

                _packageFileService
                    .Setup(x => x.SaveValidationPackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .Throws(_conflictException);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                Assert.Equal(PackageCommitResult.Conflict, result);
            }

            [Fact]
            public async Task DeletesPackageIfDatabaseCommitFailsWhenAvailable()
            {
                _package.PackageStatusKey = PackageStatus.Available;
                _package.NormalizedVersion = "3.2.1";

                _entitiesContext
                    .Setup(x => x.SaveChangesAsync())
                    .Throws(_unexpectedException);

                var exception = await Assert.ThrowsAsync(
                    _unexpectedException.GetType(),
                    () => _target.CommitPackageAsync(_package, _packageFile));

                _packageFileService.Verify(
                    x => x.DeletePackageFileAsync(Id, Version),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
                Assert.Same(_unexpectedException, exception);
            }

            [Fact]
            public async Task DeletesPackageIfDatabaseCommitFailsWhenValidating()
            {
                _package.PackageStatusKey = PackageStatus.Validating;

                _entitiesContext
                    .Setup(x => x.SaveChangesAsync())
                    .Throws(_unexpectedException);

                var exception = await Assert.ThrowsAsync(
                    _unexpectedException.GetType(),
                    () => _target.CommitPackageAsync(_package, _packageFile));

                _packageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(Id, Version),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);
                Assert.Same(_unexpectedException, exception);
            }

            [Fact]
            public async Task RejectsUploadWhenValidatingAndPackageExistsInPackagesContainer()
            {
                _package.PackageStatusKey = PackageStatus.Validating;

                _packageFileService
                    .Setup(x => x.DoesPackageFileExistAsync(It.IsAny<Package>()))
                    .ReturnsAsync(true);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _packageFileService.Verify(
                    x => x.SaveValidationPackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()),
                    Times.Never);
                _entitiesContext.Verify(
                    x => x.SaveChangesAsync(),
                    Times.Never);
                _packageFileService.Verify(
                    x => x.DoesPackageFileExistAsync(It.IsAny<Package>()),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.DeleteValidationPackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);

                Assert.Equal(PackageCommitResult.Conflict, result);
            }

            [Theory]
            [InlineData(PackageStatus.Validating, false)]
            [InlineData(PackageStatus.Available, true)]
            public async Task SavesLicenseFileWhenPackageIsAvailable(PackageStatus packageStatus, bool expectedLicenseSave)
            {
                _package.PackageStatusKey = packageStatus;
                _package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;

                _packageFile = GeneratePackage(licenseFilename: "license.txt", licenseFileContents: "some license").Object.GetStream();

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _packageFileService.Verify(
                    pfs => pfs.SaveLicenseFileAsync(_package, It.Is<Stream>(s => s != null)),
                    expectedLicenseSave ? Times.Once() : Times.Never());
            }

            [Theory]
            [InlineData(PackageStatus.Validating, false)]
            [InlineData(PackageStatus.Available, true)]
            public async Task CleansUpLicenseIfBlobSaveFails(PackageStatus packageStatus, bool expectedLicenseDelete)
            {
                _package.PackageStatusKey = packageStatus;
                _package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
                _package.NormalizedVersion = "3.2.1";

                _packageFile = GeneratePackage(licenseFilename: "license.txt", licenseFileContents: "some license").Object.GetStream();

                _packageFileService
                    .Setup(pfs => pfs.SavePackageFileAsync(_package, It.IsAny<Stream>()))
                    .ThrowsAsync(new Exception());
                _packageFileService
                    .Setup(pfs => pfs.SaveValidationPackageFileAsync(_package, It.IsAny<Stream>()))
                    .ThrowsAsync(new Exception());

                await Assert.ThrowsAsync<Exception>(() => _target.CommitPackageAsync(_package, _packageFile));

                _packageFileService.Verify(
                    pfs => pfs.DeleteLicenseFileAsync(_package.Id, _package.NormalizedVersion),
                    expectedLicenseDelete ? Times.Once() : Times.Never());
            }

            [Theory]
            [InlineData(PackageStatus.Validating, false)]
            [InlineData(PackageStatus.Available, true)]
            public async Task CleansUpLicenseIfDbUpdateFails(PackageStatus packageStatus, bool expectedLicenseDelete)
            {
                _package.PackageStatusKey = packageStatus;
                _package.EmbeddedLicenseType = EmbeddedLicenseFileType.PlainText;
                _package.NormalizedVersion = "3.2.1";

                _packageFile = GeneratePackage(licenseFilename: "license.txt", licenseFileContents: "some license").Object.GetStream();

                _entitiesContext
                    .Setup(ec => ec.SaveChangesAsync())
                    .ThrowsAsync(new Exception());

                await Assert.ThrowsAsync<Exception>(() => _target.CommitPackageAsync(_package, _packageFile));

                _packageFileService.Verify(
                    pfs => pfs.DeleteLicenseFileAsync(_package.Id, _package.NormalizedVersion.ToString()),
                    expectedLicenseDelete ? Times.Once() : Times.Never());
            }
        }

        public abstract class FactsBase
        {
            protected readonly Mock<IPackageService> _packageService;
            protected readonly Mock<IPackageFileService> _packageFileService;
            protected readonly Mock<IEntitiesContext> _entitiesContext;
            protected readonly Mock<IReservedNamespaceService> _reservedNamespaceService;
            protected readonly Mock<IValidationService> _validationService;
            protected readonly Mock<IAppConfiguration> _config;
            protected readonly Mock<ITyposquattingService> _typosquattingService;

            protected Package _package;
            protected Stream _packageFile;
            protected ArgumentException _unexpectedException;
            protected FileAlreadyExistsException _conflictException;
            protected readonly CancellationToken _token;
            protected readonly PackageUploadService _target;
            public FactsBase()
            {
                _packageService = new Mock<IPackageService>();
                _packageFileService = new Mock<IPackageFileService>();
                _entitiesContext = new Mock<IEntitiesContext>();
                _reservedNamespaceService = new Mock<IReservedNamespaceService>();
                _validationService = new Mock<IValidationService>();
                _config = new Mock<IAppConfiguration>();
                _config
                    .SetupGet(x => x.AllowLicenselessPackages)
                    .Returns(true);

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

                _target = new PackageUploadService(
                    _packageService.Object,
                    _packageFileService.Object,
                    _entitiesContext.Object,
                    _reservedNamespaceService.Object,
                    _validationService.Object,
                    _config.Object,
                    _typosquattingService.Object);
            }

            protected static Mock<TestPackageReader> GeneratePackage(
                string version = "1.2.3-alpha.0",
                RepositoryMetadata repositoryMetadata = null,
                bool isSigned = true,
                int? desiredTotalEntryCount = null,
                Func<string> getCustomNuspecNodes = null,
                Uri licenseUrl = null,
                string licenseExpression = null,
                string licenseFilename = null,
                string licenseFileContents = null,
                byte[] licenseFileBinaryContents = null)
            {
                return PackageServiceUtility.CreateNuGetPackage(
                    id: "theId",
                    version: version,
                    repositoryMetadata: repositoryMetadata,
                    isSigned: isSigned,
                    desiredTotalEntryCount: desiredTotalEntryCount,
                    getCustomNuspecNodes: getCustomNuspecNodes,
                    licenseUrl: licenseUrl,
                    licenseExpression: licenseExpression,
                    licenseFilename: licenseFilename,
                    licenseFileContents: GetBinaryLicenseFileContents(licenseFileBinaryContents, licenseFileContents));
            }

            private static byte[] GetBinaryLicenseFileContents(byte[] binaryContents, string stringContents)
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