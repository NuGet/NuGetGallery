﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.IO;
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
            var diagnosticsService = new Mock<IDiagnosticsService>();
            diagnosticsService
                .Setup(ds => ds.GetSource(It.IsAny<string>()))
                .Returns(Mock.Of<IDiagnosticsSource>());

            var packageUploadService = new Mock<PackageUploadService>(
                packageService.Object,
                new Mock<IPackageFileService>().Object,
                new Mock<IEntitiesContext>().Object,
                reservedNamespaceService.Object,
                validationService.Object,
                config.Object,
                new Mock<ITyposquattingService>().Object,
                Mock.Of<ITelemetryService>(),
                Mock.Of<ICoreLicenseFileService>(),
                diagnosticsService.Object);

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
                _nuGetPackage = GeneratePackageWithLicense(licenseUrl: null, licenseExpression: null, licenseFilename: null);
                _config
                    .Setup(x => x.AllowLicenselessPackages)
                    .Returns(allowLicenselessPackages);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

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
                    Assert.IsType<LicenseUrlDeprecationValidationMessage>(result.Warnings[0]);
                    Assert.StartsWith("All published packages should have license information specified.", result.Warnings[0].PlainTextMessage);
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
                _nuGetPackage = GeneratePackageWithLicense(
                    licenseExpression: "MIT",
                    licenseFilename: "license.txt",
                    licenseUrl: licenseUrl == null ? null : new Uri(licenseUrl));

                var ex = await Assert.ThrowsAsync<PackagingException>(() => _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage)));

                Assert.Contains("duplicate", ex.Message);
                Assert.Contains("license", ex.Message);
            }

            [Theory]
            [InlineData(false, true)]
            [InlineData(true, false)]
            public async Task HandlesLegacyLicenseUrlPackageAccordingToSettings(bool blockLegacyLicenseUrl, bool expectedSuccess)
            {
                _nuGetPackage = GeneratePackageWithLicense(licenseUrl: new Uri(RegularLicenseUrl), licenseExpression: null, licenseFilename: null);
                _config
                    .Setup(x => x.BlockLegacyLicenseUrl)
                    .Returns(blockLegacyLicenseUrl);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

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
                    Assert.StartsWith("<licenseUrl> element will be deprecated, please consider switching to specifying the license in the package.", result.Warnings[0].PlainTextMessage);
                    Assert.IsType<LicenseUrlDeprecationValidationMessage>(result.Warnings[0]);
                }
            }

            [Fact]
            public async Task RejectsLicenseDeprecationUrlWithoutLicense()
            {
                _nuGetPackage = GeneratePackageWithLicense(licenseUrl: new Uri(LicenseDeprecationUrl), licenseExpression: null, licenseFilename: null);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal("The license deprecation URL must be used in conjunction with specifying the license in the package.", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(null)]
            [InlineData(RegularLicenseUrl)]
            public async Task RejectsAlternativeLicenseUrlForLicenseFiles(string licenseUrl)
            {
                _nuGetPackage = GeneratePackageWithLicense(
                    licenseUrl: licenseUrl == null ? null : new Uri(licenseUrl),
                    licenseExpression: null,
                    licenseFilename: "license.txt",
                    licenseFileContents: "Some license text");

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

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
                _nuGetPackage = GeneratePackageWithLicense(
                    licenseUrl: new Uri($"https://licenses.nuget.org/{licenseUrlPostfix}"),
                    licenseExpression: licenseExpression,
                    licenseFilename: null,
                    licenseFileContents: null);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("malformed license URL", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData(null)]
            [InlineData(RegularLicenseUrl)]
            public async Task ErrorsWhenInvalidLicenseUrlSpecifiedWithLicenseExpression(string licenseUrl)
            {
                _nuGetPackage = GeneratePackageWithLicense(
                    licenseUrl: licenseUrl == null ? null : new Uri(licenseUrl),
                    licenseExpression: "MIT",
                    licenseFilename: null);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.IsType<InvalidLicenseUrlValidationMessage>(result.Message);
                Assert.StartsWith("To provide a better experience for older clients when a license expression is specified, <licenseUrl> must be set to 'https://licenses.nuget.org/MIT'.", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsUnlicensedPackages()
            {
                _nuGetPackage = GeneratePackageWithLicense(licenseUrl: new Uri(LicenseDeprecationUrl), licenseExpression: "UNLICENSED", licenseFilename: null);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

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
            [InlineData("(EUPL-1.1+ OR (SPL-1.0 WITH Nokia-Qt-exception-1.1) AND Sleepycat)", true)]
            public async Task ChecksLicenseExpressionCorrectness(string licenseExpression, bool expectedSuccess)
            {
                _nuGetPackage = GeneratePackageWithLicense(licenseUrl: GetLicenseExpressionDeprecationUrl(licenseExpression), licenseExpression: licenseExpression, licenseFilename: null);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

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
                _nuGetPackage = GeneratePackageWithLicense(licenseUrl: new Uri(LicenseDeprecationUrl), licenseExpression: licenseExpression, licenseFilename: null);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

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
                _nuGetPackage = GeneratePackageWithLicense(licenseUrl: licenseUri, licenseExpression: licenseExpression, licenseFilename: null);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

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
                _nuGetPackage = GeneratePackageWithLicense(licenseUrl: new Uri(LicenseDeprecationUrl), licenseExpression: licenseName, licenseFilename: null);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("deprecated", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsPackagesWithMissingLicenseFileWhenSpecified()
            {
                const string licenseFileName = "license.txt";

                _nuGetPackage = GeneratePackageWithLicense(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseExpression: null,
                    licenseFilename: licenseFileName,
                    licenseFileContents: null);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("file", result.Message.PlainTextMessage);
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

                _nuGetPackage = GeneratePackageWithLicense(
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
                    Assert.Contains("The license file has invalid extension", result.Message.PlainTextMessage);
                    Assert.Contains("Extension must be either empty or one of the following", result.Message.PlainTextMessage);
                    Assert.Contains(extension, result.Message.PlainTextMessage);
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
                _nuGetPackage = GeneratePackageWithLicense(
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
                    Assert.Contains("The license file must be plain text using UTF-8 encoding.", result.Message.PlainTextMessage);
                    Assert.Empty(result.Warnings);
                }
            }

            private static Uri GetLicenseExpressionDeprecationUrl(string licenseExpression)
            {
                return new Uri(string.Format("https://licenses.nuget.org/{0}", licenseExpression));
            }

            private static string[] LicenseNodeVariants => new string[]
            {
                "<license/>",
                "<license></license>",
                "<license> </license>",
                "<license>ttt</license>",
                "<license type='file'>fff</license>",
                "<license type='expression'>ee</license>",
                "<license type='foobar'>ttt</license>",
                "<license type='file'><someChildNode /></license>",
                "<license type='file' version='1.0.0'>fff</license>",
                "<license type='expression' version='1.0.0'>ee</license>",
                "<license type='foobar' version='1.0.0'>ttt</license>",
                "<license type='file' version='1.0.0'><someChildNode /></license>",
                "<license type='file' version='2.0.0'>fff</license>",
                "<license type='expression' version='2.0.0'>ee</license>",
                "<license type='foobar' version='2.0.0'>ttt</license>",
                "<license type='file' version='2.0.0'><someChildNode /></license>",
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
                _nuGetPackage = GeneratePackageWithLicense(getCustomNuspecNodes: () => licenseNode);

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
                    Assert.Contains("license", result.Message.PlainTextMessage);
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

                _nuGetPackage = GeneratePackageWithLicense(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseFilename: "license.txt",
                    licenseFileContents: licenseTextBuilder.ToString());

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("The license file cannot be longer", result.Message.PlainTextMessage);
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
                _nuGetPackage = GeneratePackageWithLicense(getCustomNuspecNodes: () => licenseNodeText);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
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
                _nuGetPackage = GeneratePackageWithLicense(
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"),
                    getCustomNuspecNodes: () => $"<license type='expression' version='{version}'>MIT</license>");

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
                _nuGetPackage = GeneratePackageWithLicense(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    getCustomNuspecNodes: () => licenseNodeValue);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Equal("The license node value must be shorter than 500 characters.", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Fact]
            public async Task RejectsNupkgsReportingIncorrectFileLength()
            {
                const string licenseFilename = "license.txt";
                const string licenseFileContents = "abcdefghijklnopqrstuvwxyz";

                // Arrange
                var packageStream = GeneratePackageStream(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseFilename: licenseFilename,
                    licenseFileContents: licenseFileContents);

                var buffer = packageStream.GetBuffer();

                var licenseFilenameBytes = Encoding.ASCII.GetBytes(licenseFilename);

                // the file name should appear twice in the zip stream:
                // 1. where the compressed stream is saved.
                // 2. in the central directory
                // we'll need to patch stream length in both places

                var firstInstanceOffset = FindSequenceIndex(licenseFilenameBytes, buffer);
                Assert.True(firstInstanceOffset > 0);
                var firstSizeOffset = firstInstanceOffset - 8;
                Assert.True(firstSizeOffset > 0);
                var firstLength = BitConverter.ToInt32(buffer, firstSizeOffset);
                Assert.Equal(licenseFileContents.Length, firstLength);

                var secondInstanceOffset = FindSequenceIndex(licenseFilenameBytes, buffer, firstInstanceOffset + licenseFilename.Length);
                Assert.True(secondInstanceOffset > 0);
                var secondSizeOffset = secondInstanceOffset - 22;
                Assert.True(secondSizeOffset > 0);
                var secondLength = BitConverter.ToInt32(buffer, secondSizeOffset);
                Assert.Equal(licenseFileContents.Length, secondLength);

                // now that we have offsets, we'll just patch them
                buffer[firstSizeOffset] = 1;
                buffer[secondSizeOffset] = 1;

                _nuGetPackage = PackageServiceUtility.CreateNuGetPackage(packageStream);

                // Act
                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                // Assert
                Assert.Equal(PackageValidationResultType.Invalid, result.Type);
                Assert.Contains("corrupt", result.Message.PlainTextMessage);
                Assert.Empty(result.Warnings);
            }

            [Theory]
            [InlineData("<license>foo</license>")]
            [InlineData("<license type='foo'>bar</license>")]
            public async Task RejectsNupkgsWithUnknownLicenseTypes(string licenseNode)
            {
                _nuGetPackage = GeneratePackageWithLicense(
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    getCustomNuspecNodes: () => licenseNode);

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

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
                _nuGetPackage = GeneratePackageWithLicense(
                    licenseFilename: licensePath,
                    licenseUrl: new Uri(LicenseDeprecationUrl),
                    licenseFileContents: "some license");

                var result = await _target.ValidateBeforeGeneratePackageAsync(
                    _nuGetPackage.Object,
                    GetPackageMetadata(_nuGetPackage));

                Assert.Equal(PackageValidationResultType.Accepted, result.Type);
                Assert.Null(result.Message);
                Assert.Empty(result.Warnings);
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
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

                var result = await _target.ValidateAfterGeneratePackageAsync(
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
                Assert.Equal(string.Format(Strings.TyposquattingCheckFails, string.Join(",", _typosquattingCheckCollisionIds)), result.Message.PlainTextMessage);
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
            public async Task ReturnsConflictWhenDBCommitThrowsConcurrencyViolations()
            {
                _package.PackageStatusKey = PackageStatus.Available;
                var ex = new DbUpdateConcurrencyException("whoops!");
                _entitiesContext
                    .Setup(x => x.SaveChangesAsync())
                    .Throws(ex);

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _packageFileService.Verify(
                    x => x.DeletePackageFileAsync(Id, Version),
                    Times.Once);
                _packageFileService.Verify(
                    x => x.DeletePackageFileAsync(It.IsAny<string>(), It.IsAny<string>()),
                    Times.Once);

                Assert.Equal(PackageCommitResult.Conflict, result);
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

                _packageFile = GeneratePackageWithLicense(licenseFilename: "license.txt", licenseFileContents: "some license").Object.GetStream();

                var result = await _target.CommitPackageAsync(_package, _packageFile);

                _licenseFileService.Verify(
                    lfs => lfs.ExtractAndSaveLicenseFileAsync(_package, _packageFile),
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

                _packageFile = GeneratePackageWithLicense(licenseFilename: "license.txt", licenseFileContents: "some license").Object.GetStream();

                _packageFileService
                    .Setup(pfs => pfs.SavePackageFileAsync(_package, It.IsAny<Stream>()))
                    .ThrowsAsync(new Exception());
                _packageFileService
                    .Setup(pfs => pfs.SaveValidationPackageFileAsync(_package, It.IsAny<Stream>()))
                    .ThrowsAsync(new Exception());

                await Assert.ThrowsAsync<Exception>(() => _target.CommitPackageAsync(_package, _packageFile));

                _licenseFileService.Verify(
                    lfs => lfs.DeleteLicenseFileAsync(_package.Id, _package.NormalizedVersion),
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

                _packageFile = GeneratePackageWithLicense(licenseFilename: "license.txt", licenseFileContents: "some license").Object.GetStream();

                _entitiesContext
                    .Setup(ec => ec.SaveChangesAsync())
                    .ThrowsAsync(new Exception());

                await Assert.ThrowsAsync<Exception>(() => _target.CommitPackageAsync(_package, _packageFile));

                _licenseFileService.Verify(
                    lfs => lfs.DeleteLicenseFileAsync(_package.Id, _package.NormalizedVersion.ToString()),
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
            protected readonly Mock<ITelemetryService> _telemetryService;
            protected readonly Mock<ICoreLicenseFileService> _licenseFileService;
            protected readonly Mock<IDiagnosticsService> _diagnosticsService;
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
                _licenseFileService = new Mock<ICoreLicenseFileService>();
                _diagnosticsService = new Mock<IDiagnosticsService>();

                _diagnosticsService
                    .Setup(ds => ds.GetSource(It.IsAny<string>()))
                    .Returns(Mock.Of<IDiagnosticsSource>());

                _target = new PackageUploadService(
                    _packageService.Object,
                    _packageFileService.Object,
                    _entitiesContext.Object,
                    _reservedNamespaceService.Object,
                    _validationService.Object,
                    _config.Object,
                    _typosquattingService.Object,
                    _telemetryService.Object,
                    _licenseFileService.Object,
                    _diagnosticsService.Object);
            }

            protected static Mock<TestPackageReader> GeneratePackage(
                string version = "1.2.3-alpha.0",
                RepositoryMetadata repositoryMetadata = null,
                bool isSigned = true,
                int? desiredTotalEntryCount = null,
                Func<string> getCustomNuspecNodes = null)
                => GeneratePackageWithLicense(
                    version: version,
                    repositoryMetadata: repositoryMetadata,
                    isSigned: isSigned,
                    desiredTotalEntryCount: desiredTotalEntryCount,
                    getCustomNuspecNodes: getCustomNuspecNodes,
                    licenseUrl: new Uri("https://licenses.nuget.org/MIT"),
                    licenseExpression: "MIT",
                    licenseFilename: null,
                    licenseFileContents: null,
                    licenseFileBinaryContents: null);

            protected static Mock<TestPackageReader> GeneratePackageWithLicense(
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
                var packageStream = GeneratePackageStream(
                    version: version,
                    repositoryMetadata: repositoryMetadata,
                    isSigned: isSigned,
                    desiredTotalEntryCount: desiredTotalEntryCount,
                    getCustomNuspecNodes: getCustomNuspecNodes,
                    licenseUrl: licenseUrl,
                    licenseExpression: licenseExpression,
                    licenseFilename: licenseFilename,
                    licenseFileContents: licenseFileContents,
                    licenseFileBinaryContents: licenseFileBinaryContents);

                return PackageServiceUtility.CreateNuGetPackage(packageStream);
            }

            protected static MemoryStream GeneratePackageStream(
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
                return PackageServiceUtility.CreateNuGetPackageStream(
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