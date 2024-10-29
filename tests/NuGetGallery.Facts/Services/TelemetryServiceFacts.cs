// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Diagnostics;
using NuGetGallery.Framework;
using Xunit;

using TrackAction = System.Action<NuGetGallery.TelemetryService>;

namespace NuGetGallery
{
    public class TelemetryServiceFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void ThrowsForNullDiagnosticsSource()
            {
                Assert.Throws<ArgumentNullException>(() => new TelemetryService(null, new Mock<ITelemetryClient>().Object));
            }

            [Fact]
            public void ThrowsForNullTelemetryClient()
            {
                Assert.Throws<ArgumentNullException>(() => new TelemetryService(new Mock<IDiagnosticsSource>().Object, null));
            }
        }

        public class TheTrackEventMethod : BaseFacts
        {
            private static Fakes fakes = new Fakes();

            public static IEnumerable<object[]> TrackMetricNames_Data
            {
                get
                {
                    var packages = fakes.Package.Packages.ToList();
                    var package = packages.First();
                    var identity = Fakes.ToIdentity(fakes.User);
                    yield return new object[] { "CertificateActivated",
                        (TrackAction)(s => s.TrackCertificateActivated("thumbprint"))
                    };

                    yield return new object[] { "CertificateAdded",
                        (TrackAction)(s => s.TrackCertificateAdded("thumbprint"))
                    };

                    yield return new object[] { "CertificateDeactivated",
                        (TrackAction)(s => s.TrackCertificateDeactivated("thumbprint"))
                    };

                    yield return new object[] { "PackageRegistrationRequiredSignerSet",
                        (TrackAction)(s => s.TrackRequiredSignerSet(package.PackageRegistration.Id))
                    };

                    yield return new object[] { "DownloadJsonRefreshDuration",
                        (TrackAction)(s => s.TrackDownloadJsonRefreshDuration(TimeSpan.FromMilliseconds(0)))
                    };

                    yield return new object[] { "DownloadCountDecreasedDuringRefresh",
                        (TrackAction)(s => s.TrackDownloadCountDecreasedDuringRefresh(package.PackageRegistration.Id, package.Version, 0, 0))
                    };

                    yield return new object[] { "GalleryDownloadGreaterThanJsonForPackage",
                        (TrackAction)(s => s.TrackPackageDownloadCountDecreasedFromGallery(package.PackageRegistration.Id, package.Version, 0, 0))
                    };

                    yield return new object[] { "GalleryDownloadGreaterThanJsonForPackageRegistration",
                        (TrackAction)(s => s.TrackPackageRegistrationDownloadCountDecreasedFromGallery(package.PackageRegistration.Id, 0, 0))
                    };

                    yield return new object[] { "GetPackageDownloadCountFailed",
                        (TrackAction)(s => s.TrackGetPackageDownloadCountFailed(package.PackageRegistration.Id, package.Version))
                    };

                    yield return new object[] { "GetPackageRegistrationDownloadCountFailed",
                        (TrackAction)(s => s.TrackGetPackageRegistrationDownloadCountFailed(package.PackageRegistration.Id))
                    };

                    yield return new object[] { "ODataQueryFilter",
                        (TrackAction)(s => s.TrackODataQueryFilterEvent("callContext", true, true, "queryPattern"))
                    };

                    yield return new object[] { "ODataCustomQuery",
                        (TrackAction)(s => s.TrackODataCustomQuery(true))
                    };

                    yield return new object[] { "PackagePush",
                        (TrackAction)(s => s.TrackPackagePushEvent(package, fakes.User, identity))
                    };

                    yield return new object[] { "PackagePushFailure",
                        (TrackAction)(s => s.TrackPackagePushFailureEvent("id", new NuGetVersion("1.2.3")))
                    };

                    yield return new object[] { "PackageUnlisted",
                        (TrackAction)(s => s.TrackPackageUnlisted(package))
                    };

                    yield return new object[] { "PackageListed",
                        (TrackAction)(s => s.TrackPackageListed(package))
                    };

                    yield return new object[] { "PackageDelete",
                        (TrackAction)(s => s.TrackPackageDelete(package, isHardDelete: true))
                    };

                    yield return new object[] { "PackagesUpdateListed",
                        (TrackAction)(s => s.TrackPackagesUpdateListed(new[] { package }, listed: true))
                    };

                    yield return new object[] { "PackageReupload",
                        (TrackAction)(s => s.TrackPackageReupload(package))
                    };

                    yield return new object[] { "PackageReflow",
                        (TrackAction)(s => s.TrackPackageReflow(package))
                    };

                    yield return new object[] { "PackageHardDeleteReflow",
                        (TrackAction)(s => s.TrackPackageHardDeleteReflow(fakes.Package.Id, package.Version))
                    };

                    yield return new object[] { "PackageRevalidate",
                        (TrackAction)(s => s.TrackPackageRevalidate(package))
                    };

                    yield return new object[] { "PackageDeprecate",
                        (TrackAction)(s => s.TrackPackageDeprecate(
                            packages,
                            PackageDeprecationStatus.Legacy,
                            new PackageRegistration { Id = "alt" },
                            new Package { PackageRegistration = new PackageRegistration { Id = "alt-2" }, NormalizedVersion = "1.2.3" }, true, false))
                    };

                    yield return new object[] { "CreatePackageVerificationKey",
                        (TrackAction)(s => s.TrackCreatePackageVerificationKeyEvent(fakes.Package.Id, package.Version, fakes.User, identity))
                    };

                    yield return new object[] { "VerifyPackageKey",
                        (TrackAction)(s => s.TrackVerifyPackageKeyEvent(fakes.Package.Id, package.Version, fakes.User, identity, 0))
                    };

                    yield return new object[] { "PackageReadMeChanged",
                        (TrackAction)(s => s.TrackPackageReadMeChangeEvent(package, "written", PackageEditReadMeState.Changed))
                    };

                    yield return new object[] { "PackagePushNamespaceConflict",
                        (TrackAction)(s => s.TrackPackagePushNamespaceConflictEvent(fakes.Package.Id, package.Version, fakes.User, identity))
                    };

                    yield return new object[] { "PackagePushOwnerlessNamespaceConflict",
                        (TrackAction)(s => s.TrackPackagePushOwnerlessNamespaceConflictEvent(fakes.Package.Id, package.Version, fakes.User, identity))
                    };

                    yield return new object[] { "NewUserRegistration",
                        (TrackAction)(s => s.TrackNewUserRegistrationEvent(fakes.User, fakes.User.Credentials.First()))
                    };

                    yield return new object[] { "UserMultiFactorAuthenticationEnabled",
                        (TrackAction)(s => s.TrackUserChangedMultiFactorAuthentication(fakes.User, enabledMultiFactorAuth: true))
                    };
                    yield return new object[] { "UserMultiFactorAuthenticationDisabled",
                        (TrackAction)(s => s.TrackUserChangedMultiFactorAuthentication(fakes.User, enabledMultiFactorAuth: false))
                    };

                    yield return new object[] { "CredentialAdded",
                        (TrackAction)(s => s.TrackNewCredentialCreated(fakes.User, fakes.User.Credentials.First()))
                    };

                    yield return new object[] { "CredentialUsed",
                        (TrackAction)(s => s.TrackUserLogin(fakes.User, fakes.User.Credentials.First(), wasMultiFactorAuthenticated: true))
                    };

                    yield return new object[] { "UserPackageDeleteCheckedAfterHours",
                        (TrackAction)(s => s.TrackUserPackageDeleteChecked(
                            new UserPackageDeleteEvent(
                                TimeSpan.FromHours(3),
                                11,
                                "NuGet.Versioning",
                                "4.5.0",
                                124101,
                                124999,
                                23,
                                42,
                                reportPackageReason: ReportPackageReason.ReleasedInPublicByAccident,
                                packageDeleteDecision: PackageDeleteDecision.DeletePackage),
                            UserPackageDeleteOutcome.Accepted))
                    };

                    yield return new object[] { "UserPackageDeleteExecuted",
                        (TrackAction)(s => s.TrackUserPackageDeleteExecuted(
                            11,
                            "NuGet.Versioning",
                            "4.5.0",
                            ReportPackageReason.ReleasedInPublicByAccident,
                            success: true))
                    };

                    yield return new object[] { "OrganizationTransformInitiated",
                        (TrackAction)(s => s.TrackOrganizationTransformInitiated(fakes.User))
                    };

                    yield return new object[] { "OrganizationTransformCompleted",
                        (TrackAction)(s => s.TrackOrganizationTransformCompleted(fakes.Organization))
                    };

                    yield return new object[] { "OrganizationTransformDeclined",
                        (TrackAction)(s => s.TrackOrganizationTransformDeclined(fakes.User))
                    };

                    yield return new object[] { "OrganizationTransformCancelled",
                        (TrackAction)(s => s.TrackOrganizationTransformCancelled(fakes.User))
                    };

                    yield return new object[] { "OrganizationAdded",
                        (TrackAction)(s => s.TrackOrganizationAdded(fakes.Organization))
                    };

                    yield return new object[] { "AccountDeleteCompleted",
                        (TrackAction)(s => s.TrackAccountDeletionCompleted(fakes.User, fakes.User, true))
                    };

                    yield return new object[] { "AccountDeleteRequested",
                        (TrackAction)(s => s.TrackRequestForAccountDeletion(fakes.User))
                    };

                    yield return new object[] { "SymbolPackagePush",
                        (TrackAction)(s => s.TrackSymbolPackagePushEvent("id", "version"))
                    };

                    yield return new object[] { "SymbolPackageDelete",
                        (TrackAction)(s => s.TrackSymbolPackageDeleteEvent("id", "version"))
                    };

                    yield return new object[] { "SymbolPackagePushFailure",
                        (TrackAction)(s => s.TrackSymbolPackagePushFailureEvent("id", "version"))
                    };

                    yield return new object[] { "SymbolPackageGalleryValidation",
                        (TrackAction)(s => s.TrackSymbolPackageFailedGalleryValidationEvent("id", "version"))
                    };

                    yield return new object[] { "SymbolPackageRevalidate",
                        (TrackAction)(s => s.TrackSymbolPackageRevalidate("id", "version"))
                    };

                    yield return new object[] { "PackageMetadataComplianceError",
                        (TrackAction)(s => s.TrackPackageMetadataComplianceError(fakes.Package.Id, package.NormalizedVersion, new[] { "Failure reason" }))
                    };

                    yield return new object[] { "PackageMetadataComplianceWarning",
                        (TrackAction)(s => s.TrackPackageMetadataComplianceWarning(fakes.Package.Id, package.NormalizedVersion, new[] { "Warning message" }))
                    };

                    yield return new object[] { "PackageOwnershipAutomaticallyAdded",
                        (TrackAction)(s => s.TrackPackageOwnershipAutomaticallyAdded(fakes.Package.Id, package.NormalizedVersion))
                    };

                    yield return new object[] { "TyposquattingCheckResultAndTotalTimeInMs",
                        (TrackAction)(s => s.TrackMetricForTyposquattingCheckResultAndTotalTime(fakes.Package.Id, TimeSpan.FromMilliseconds(100), true, new List<string>{"newtonsoft-json" }, 10000, TimeSpan.FromHours(24)))
                    };

                    yield return new object[] { "TyposquattingChecklistRetrievalTimeInMs",
                        (TrackAction)(s => s.TrackMetricForTyposquattingChecklistRetrievalTime(fakes.Package.Id, TimeSpan.FromMilliseconds(100)))
                    };

                    yield return new object[] { "TyposquattingAlgorithmProcessingTimeInMs",
                        (TrackAction)(s => s.TrackMetricForTyposquattingAlgorithmProcessingTime(fakes.Package.Id, TimeSpan.FromMilliseconds(100)))
                    };

                    yield return new object[] { "TyposquattingOwnersCheckTimeInMs",
                        (TrackAction)(s => s.TrackMetricForTyposquattingOwnersCheckTime(fakes.Package.Id, TimeSpan.FromMilliseconds(100)))
                    };

                    yield return new object[] { "InvalidLicenseMetadata",
                        (TrackAction)(s => s.TrackInvalidLicenseMetadata("foo"))
                    };

                    yield return new object[] { "NonFsfOsiLicenseUsed",
                        (TrackAction)(s => s.TrackNonFsfOsiLicenseUse("foo"))
                    };

                    yield return new object[] { "LicenseFileRejected",
                        (TrackAction)(s => s.TrackLicenseFileRejected())
                    };

                    yield return new object[] { "LicenseValidationFailed",
                        (TrackAction)(s => s.TrackLicenseValidationFailure())
                    };

                    yield return new object[] { "FeatureFlagStalenessSeconds",
                        (TrackAction)(s => s.TrackFeatureFlagStaleness(TimeSpan.FromMilliseconds(100)))
                    };

                    yield return new object[] { "SearchExecutionDuration",
                        (TrackAction)(s => s.TrackMetricForSearchExecutionDuration("https://www.bing.com", TimeSpan.FromMilliseconds(100), true))
                    };

                    yield return new object[] { "SearchCircuitBreakerOnBreak",
                        (TrackAction)(s => s.TrackMetricForSearchCircuitBreakerOnBreak("SomeName", exception: null, responseMessage: null, correlationId: string.Empty, uri: string.Empty))
                    };

                    yield return new object[] { "SearchCircuitBreakerOnReset",
                        (TrackAction)(s => s.TrackMetricForSearchCircuitBreakerOnReset("SomeName", correlationId: string.Empty, uri: string.Empty))
                    };

                    yield return new object[] { "SearchOnRetry",
                        (TrackAction)(s => s.TrackMetricForSearchOnRetry("SomeName", exception: null, correlationId: string.Empty, uri: string.Empty, circuitBreakerStatus: string.Empty))
                    };

                    yield return new object[] { "SearchOnTimeout",
                        (TrackAction)(s => s.TrackMetricForSearchOnTimeout("SomeName", correlationId: string.Empty, uri: string.Empty, circuitBreakerStatus: string.Empty))
                    };

                    yield return new object[] { "SearchSideBySideFeedback",
                        (TrackAction)(s => s.TrackSearchSideBySideFeedback("nuget", 1, 2, "new", "nuget.core", null, true, true))
                    };

                    yield return new object[] { "SearchSideBySide",
                        (TrackAction)(s => s.TrackSearchSideBySide("nuget", true, 1, true, 2))
                    };

                    yield return new object[] { "ABTestEnrollmentInitialized",
                        (TrackAction)(s => s.TrackABTestEnrollmentInitialized(2, 42, 47))
                    };

                    yield return new object[] { "ABTestEnrollmentUpgraded",
                        (TrackAction)(s => s.TrackABTestEnrollmentUpgraded(1, 2, 42, 47))
                    };

                    yield return new object[] { "ABTestEvaluated",
                        (TrackAction)(s => s.TrackABTestEvaluated("SearchPreview", true, true, 0, 20))
                    };

                    yield return new object[] { "PackagePushDisconnect",
                        (TrackAction)(s => s.TrackPackagePushDisconnectEvent())
                    };

                    yield return new object[] { "SymbolPackagePushDisconnect",
                        (TrackAction)(s => s.TrackSymbolPackagePushDisconnectEvent())
                    };

                    yield return new object[] { "VulnerabilitiesCacheRefreshDurationMs",
                        (TrackAction)(s => s.TrackVulnerabilitiesCacheRefreshDuration(TimeSpan.FromMilliseconds(0)))
                    };

                    yield return new object[] { "InstanceUptimeInDays",
                        (TrackAction)(s => s.TrackInstanceUptime(TimeSpan.FromSeconds(1)))
                    };

                    yield return new object[] { "ApiRequest",
                        (TrackAction)(s => s.TrackApiRequest("SomeEndpoint")),
                        true
                    };

                    yield return new object[] { "CreateSqlConnectionDurationMs",
                        (TrackAction)(s => s.TrackSyncSqlConnectionCreationDuration().Dispose()),
                        true
                    };

                    yield return new object[] { "CreateSqlConnectionDurationMs",
                        (TrackAction)(s => s.TrackAsyncSqlConnectionCreationDuration().Dispose()),
                        true
                    };
                }
            }

            [Fact]
            public void TrackEventNamesIncludesAllEvents()
            {
                var eventNames = typeof(TelemetryService.Events)
                    .GetFields()
                    .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                    .Select(f => (string)f.GetValue(null))
                    .ToList();

                var testedNames = new HashSet<string>(TrackMetricNames_Data.Select(element => (string)element[0]));

                Assert.All(eventNames, name => testedNames.Contains(name));
            }

            [Theory]
            [MemberData(nameof(TrackMetricNames_Data))]
            public void TrackMetricNames(string metricName, TrackAction track, bool isAggregatedMetric = false)
            {
                // Arrange
                var service = CreateService();

                // Act
                track(service);

                // Assert
                if (!isAggregatedMetric)
                {
                    service.TelemetryClient.Verify(c => c.TrackMetric(metricName,
                        It.IsAny<double>(),
                        It.IsAny<IDictionary<string, string>>()),
                        Times.Once);
                }
                else
                {
                    service.TelemetryClient.Verify(c => c.TrackAggregatedMetric(metricName,
                        It.IsAny<double>(),
                        It.IsAny<string>(), It.IsAny<string>()),
                        Times.Once);
                }
            }

            [Fact]
            public void TrackPackageReadMeChangeEventThrowsIfPackageIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackPackageReadMeChangeEvent(null, "written", PackageEditReadMeState.Changed));
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void TrackPackageReadMeChangeEventThrowsIfSourceTypeIsNullOrEmpty(string sourceType)
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackPackageReadMeChangeEvent(fakes.Package.Packages.First(), sourceType, PackageEditReadMeState.Changed));
            }

            [Fact]
            public void TrackPackagePushEventThrowsIfPackageIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackPackagePushEvent(null, fakes.User, Fakes.ToIdentity(fakes.User)));
            }

            [Fact]
            public void TrackPackagePushEventThrowsIfUserIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackPackagePushEvent(fakes.Package.Packages.First(), null, Fakes.ToIdentity(fakes.User)));
            }

            [Fact]
            public void TrackPackagePushEventThrowsIfIdentityIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackPackagePushEvent(fakes.Package.Packages.First(), fakes.User, null));
            }

            [Fact]
            public void TrackCreatePackageVerificationKeyEventThrowsIfUserIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackCreatePackageVerificationKeyEvent("id", "1.0.0", null, Fakes.ToIdentity(fakes.User)));
            }

            [Fact]
            public void TrackCreatePackageVerificationKeyEventThrowsIfIdentityIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackCreatePackageVerificationKeyEvent("id", "1.0.0", fakes.User, null));
            }

            [Fact]
            public void TrackVerifyPackageKeyEventThrowsIfUserIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackVerifyPackageKeyEvent("id", "1.0.0", null, Fakes.ToIdentity(fakes.User), 200));
            }

            [Fact]
            public void TrackVerifyPackageKeyEventThrowsIfIdentityIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackVerifyPackageKeyEvent("id", "1.0.0", fakes.User, null, 200));
            }

            [Fact]
            public void TrackUserPackageDeleteCheckedThrowsIfDetailsAreNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackUserPackageDeleteChecked(details: null, outcome: UserPackageDeleteOutcome.Accepted));
            }

            [Fact]
            public void TrackUserPackageDeleteExecutedThrowsIfPackageIdIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackUserPackageDeleteExecuted(
                        23,
                        packageId: null,
                        packageVersion: "4.5.0",
                        reason: ReportPackageReason.ReleasedInPublicByAccident,
                        success: true));
            }

            [Fact]
            public void TrackUserPackageDeleteExecutedThrowsIfPackageVersionIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackUserPackageDeleteExecuted(
                        23,
                        packageId: "NuGet.Versioning",
                        packageVersion: null,
                        reason: ReportPackageReason.ReleasedInPublicByAccident,
                        success: true));
            }

            [Fact]
            public void TrackOrganizationTransformInitiatedThrowsIfNullUser()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackOrganizationTransformInitiated(null));
            }

            [Fact]
            public void TrackOrganizationTransformCompletedThrowsIfNullOrganization()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackOrganizationTransformCompleted(null));
            }

            [Fact]
            public void TrackOrganizationTransformDeclinedThrowsIfNullUser()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackOrganizationTransformDeclined(null));
            }

            [Fact]
            public void TrackOrganizationTransformCancelledThrowsIfNullUser()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackOrganizationTransformCancelled(null));
            }

            [Fact]
            public void TrackOrganizationAddedThrowsIfNullUser()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackOrganizationAdded(null));
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void TrackCertificateAdded_WhenThumbprintIsInvalid_Throws(string thumbprint)
            {
                var service = CreateService();
                var exception = Assert.Throws<ArgumentException>(
                    () => service.TrackCertificateAdded(thumbprint));

                Assert.Equal("thumbprint", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }

            [Fact]
            public void TrackCertificateAdded_WhenThumbprintIsValid_Throws()
            {
                const string thumbprint = "a";

                var service = CreateServiceForCertificateTelemetry("CertificateAdded", thumbprint);

                service.TrackCertificateAdded(thumbprint);

                service.TelemetryClient.VerifyAll();
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void TrackCertificateActivated_WhenThumbprintIsInvalid_Throws(string thumbprint)
            {
                var service = CreateService();
                var exception = Assert.Throws<ArgumentException>(
                    () => service.TrackCertificateActivated(thumbprint));

                Assert.Equal("thumbprint", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }

            [Fact]
            public void TrackCertificateActivated_WhenThumbprintIsValid_Throws()
            {
                const string thumbprint = "a";

                var service = CreateServiceForCertificateTelemetry("CertificateActivated", thumbprint);

                service.TrackCertificateActivated(thumbprint);

                service.TelemetryClient.VerifyAll();
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void TrackCertificateDeactivated_WhenThumbprintIsInvalid_Throws(string thumbprint)
            {
                var service = CreateService();
                var exception = Assert.Throws<ArgumentException>(
                    () => service.TrackCertificateDeactivated(thumbprint));

                Assert.Equal("thumbprint", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }

            [Fact]
            public void TrackCertificateDeactivated_WhenThumbprintIsValid_Throws()
            {
                const string thumbprint = "a";

                var service = CreateServiceForCertificateTelemetry("CertificateDeactivated", thumbprint);

                service.TrackCertificateDeactivated(thumbprint);

                service.TelemetryClient.VerifyAll();
            }

            [Theory]
            [InlineData(null)]
            [InlineData("")]
            public void TrackRequiredSignerSet_WhenPackageRegistrationIsInvalid_Throws(string packageId)
            {
                var service = CreateService();
                var exception = Assert.Throws<ArgumentException>(
                    () => service.TrackRequiredSignerSet(packageId));

                Assert.Equal("packageId", exception.ParamName);
                Assert.StartsWith("The argument cannot be null or empty.", exception.Message);
            }

            [Fact]
            public void TrackRequiredSignerSet_WhenPackageRegistrationIsValid_Throws()
            {
                const string packageId = "a";

                var service = CreateService();

                service.TelemetryClient.Setup(
                   x => x.TrackMetric(
                       It.Is<string>(name => name == "PackageRegistrationRequiredSignerSet"),
                       It.Is<double>(value => value == 1),
                       It.Is<IDictionary<string, string>>(
                           properties => properties.Count == 1 &&
                               properties["PackageId"] == packageId)));

                service.TrackRequiredSignerSet(packageId);

                service.TelemetryClient.VerifyAll();
            }

            [Fact]
            public void TrackAccountDeletedCompletedThrowsIfNullUser()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackAccountDeletionCompleted(null, fakes.User, true));

                Assert.Throws<ArgumentNullException>(() =>
                   service.TrackAccountDeletionCompleted(fakes.User, null, true));
            }

            [Fact]
            public void TrackAccountDeletedCompletedAddsCorrectData()
            {
                var service = CreateService();

                service.TelemetryClient.Setup(
                   x => x.TrackMetric(
                       It.Is<string>(name => name == "AccountDeleteCompleted"),
                       It.Is<double>(value => value == 1),
                       It.Is<IDictionary<string, string>>(
                           properties => properties.Count == 4 &&
                               properties["AccountDeletedByRole"] == "[\"Admins\"]" &&
                               properties["AccountIsSelfDeleted"] == "False" &&
                               properties["AccountDeletedIsOrganization"] == "True" &&
                               properties["AccountDeleteSucceeded"] == "True")
                               ));

                service.TrackAccountDeletionCompleted(fakes.Organization, fakes.Admin, true);

                service.TelemetryClient.VerifyAll();
            }

            [Fact]
            public void TrackRequestForAccountDeletedThrowsIfNullUser()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() =>
                    service.TrackRequestForAccountDeletion(null));
            }

            [Fact]
            public void TrackRequestForAccountDeletedAddsCorrectData()
            {
                var service = CreateService();
                var user = fakes.User;

                service.TelemetryClient.Setup(
                   x => x.TrackMetric(
                       It.Is<string>(name => name == "AccountDeleteRequested"),
                       It.Is<double>(value => value == 1),
                       It.Is<IDictionary<string, string>>(
                           properties => properties.Count == 1 &&
                               properties["CreatedDateForAccountToBeDeleted"] == $"{user.CreatedUtc}"
                               )));

                service.TrackRequestForAccountDeletion(fakes.User);

                service.TelemetryClient.VerifyAll();
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void TrackODataCustomQueryAddsCorrectData(bool customQuery)
            {
                var service = CreateService();
                var allProperties = new List<IDictionary<string, string>>();
                service.TelemetryClient
                    .Setup(x => x.TrackMetric(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<IDictionary<string, string>>()))
                    .Callback<string, double, IDictionary<string, string>>((_, __, p) => allProperties.Add(p));

                service.TrackODataCustomQuery(customQuery);

                service.TelemetryClient.Verify(
                    x => x.TrackMetric("ODataCustomQuery", 1, It.IsAny<IDictionary<string, string>>()),
                    Times.Once);
                var properties = Assert.Single(allProperties);
                Assert.Contains("IsCustomQuery", properties.Keys);
                Assert.Equal(customQuery.ToString(), properties["IsCustomQuery"]);
            }

            public static IEnumerable<object[]> TrackPackageDeprecateThrowsIfPackageListInvalid_Data =>
                new[]
                {
                    new object[]
                    {
                        null
                    },
                    new object[]
                    {
                        Array.Empty<Package>()
                    },
                    new object[]
                    {
                        new []
                        {
                            new Package { PackageRegistrationKey = 1 },
                            new Package { PackageRegistrationKey = 2 }
                        }
                    }
                };

            [Theory]
            [MemberData(nameof(TrackPackageDeprecateThrowsIfPackageListInvalid_Data))]
            public void TrackPackageDeprecateThrowsIfPackageListInvalid(IReadOnlyList<Package> packages)
            {
                var service = CreateService();
                Assert.Throws<ArgumentException>(() => service.TrackPackageDeprecate(packages, PackageDeprecationStatus.CriticalBugs, null, null, false, false));
            }

            public static IEnumerable<object[]> TrackPackageDeprecateSucceedsWithoutAlternate_Data =>
                MemberDataHelper.Combine(
                    MemberDataHelper.EnumDataSet<PackageDeprecationStatus>(),
                    MemberDataHelper.BooleanDataSet());

            [Theory]
            [MemberData(nameof(TrackPackageDeprecateSucceedsWithoutAlternate_Data))]
            public void TrackPackageDeprecateSucceedsWithoutAlternate(PackageDeprecationStatus status, bool hasCustomMessage)
            {
                var service = CreateService();
                var packages = fakes.Package.Packages.ToList();
                var allProperties = new List<IDictionary<string, string>>();
                service.TelemetryClient
                    .Setup(x => x.TrackMetric(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<IDictionary<string, string>>()))
                    .Callback<string, double, IDictionary<string, string>>((_, __, p) => allProperties.Add(p));

                service.TrackPackageDeprecate(packages, status, null, null, hasCustomMessage, true);

                service.TelemetryClient.Verify(
                    x => x.TrackMetric("PackageDeprecate", packages.Count(), It.IsAny<IDictionary<string, string>>()),
                    Times.Once);

                var properties = Assert.Single(allProperties);
                Assert.Contains(
                    new KeyValuePair<string, string>("PackageDeprecationReason", ((int)status).ToString()),
                    properties);

                Assert.Contains(
                    new KeyValuePair<string, string>("PackageDeprecationAlternatePackageId", null),
                    properties);

                Assert.Contains(
                    new KeyValuePair<string, string>("PackageDeprecationAlternatePackageVersion", null),
                    properties);

                Assert.Contains(
                    new KeyValuePair<string, string>("PackageDeprecationCustomMessage", hasCustomMessage.ToString()),
                    properties);
            }

            public static IEnumerable<object[]> TrackPackageDeprecateSucceedsWithAlternate_Data =>
                MemberDataHelper.Combine(
                    MemberDataHelper.BooleanDataSet(),
                    MemberDataHelper.BooleanDataSet());

            [Theory]
            [MemberData(nameof(TrackPackageDeprecateSucceedsWithAlternate_Data))]
            public void TrackPackageDeprecateSucceedsWithAlternate(bool hasRegistration, bool hasPackage)
            {
                var service = CreateService();
                var allProperties = new List<IDictionary<string, string>>();
                service.TelemetryClient
                    .Setup(x => x.TrackMetric(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<IDictionary<string, string>>()))
                    .Callback<string, double, IDictionary<string, string>>((_, __, p) => allProperties.Add(p));

                var packages = fakes.Package.Packages.ToList();
                var alternateRegistration = hasRegistration ? new PackageRegistration { Id = "alt-R" } : null;
                var alternatePackage = hasPackage ? new Package { PackageRegistration = new PackageRegistration { Id = "alt-P" }, NormalizedVersion = "4.3.2" } : null;

                var status = PackageDeprecationStatus.NotDeprecated;
                service.TrackPackageDeprecate(packages, status, alternateRegistration, alternatePackage, false, true);

                service.TelemetryClient.Verify(
                    x => x.TrackMetric("PackageDeprecate", packages.Count(), It.IsAny<IDictionary<string, string>>()),
                    Times.Once);

                var properties = Assert.Single(allProperties);
                Assert.Contains(
                    new KeyValuePair<string, string>("PackageDeprecationReason", ((int)PackageDeprecationStatus.NotDeprecated).ToString()),
                    properties);

                var expectedAlternateId = hasRegistration
                    ? alternateRegistration.Id
                    : (hasPackage ? alternatePackage.Id : null);

                Assert.Contains(
                    new KeyValuePair<string, string>("PackageDeprecationAlternatePackageId", expectedAlternateId),
                    properties);

                var expectedAlternateVersion = hasPackage ? alternatePackage.NormalizedVersion : null;

                Assert.Contains(
                    new KeyValuePair<string, string>("PackageDeprecationAlternatePackageVersion", expectedAlternateVersion),
                    properties);

                Assert.Contains(
                    new KeyValuePair<string, string>("PackageDeprecationCustomMessage", false.ToString()),
                    properties);
            }

            private TelemetryServiceWrapper CreateServiceForCertificateTelemetry(string metricName, string thumbprint)
            {
                var service = CreateService();

                service.TelemetryClient.Setup(
                   x => x.TrackMetric(
                       It.Is<string>(name => name == metricName),
                       It.Is<double>(value => value == 1),
                       It.Is<IDictionary<string, string>>(
                           properties => properties.Count == 1 &&
                               properties["Sha256Thumbprint"] == thumbprint)));

                return service;
            }
        }

        public class TheTraceExceptionMethod : BaseFacts
        {
            [Fact]
            public void ThrowsIfExceptionIsNull()
            {
                // Arrange
                var service = CreateService();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => service.TraceException(null));
            }

            [Fact]
            public void CallsTraceEvent()
            {
                // Arrange
                var service = CreateService();

                // Act
                service.TraceException(new InvalidOperationException("Example"));

                // Assert
                service.TraceSource.Verify(t => t.TraceEvent(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()),
                    Times.Once);
                Assert.Contains("InvalidOperationException", service.LastTraceMessage);
            }
        }

        public class BaseFacts
        {
            public class TelemetryServiceWrapper : TelemetryService
            {
                public TelemetryServiceWrapper(IDiagnosticsSource diagnosticsSource, ITelemetryClient telemetryClient)
                    : base(diagnosticsSource, telemetryClient)
                {
                }

                public Mock<IDiagnosticsSource> TraceSource { get; set; }

                public Mock<ITelemetryClient> TelemetryClient { get; set; }

                public string LastTraceMessage { get; set; }
            }

            public static TelemetryServiceWrapper CreateService()
            {
                var traceSource = new Mock<IDiagnosticsSource>();
                var telemetryClient = new Mock<ITelemetryClient>();

                var telemetryService = new TelemetryServiceWrapper(traceSource.Object, telemetryClient.Object);
                telemetryService.TraceSource = traceSource;
                telemetryService.TelemetryClient = telemetryClient;

                traceSource.Setup(t => t.TraceEvent(
                        It.IsAny<LogLevel>(),
                        It.IsAny<EventId>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()))
                    .Callback<LogLevel, EventId, string, string, string, int>(
                        (type, id, message, member, file, line) => telemetryService.LastTraceMessage = message)
                    .Verifiable();

                return telemetryService;
            }
        }
    }
}
