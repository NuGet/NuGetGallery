// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Moq;
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
            public void ThrowsIfDiagnosticsServiceIsNull()
            {
                Assert.Throws<ArgumentNullException>(() => new TelemetryService(null));
            }
        }

        public class TheTrackEventMethod : BaseFacts
        {
            private static Fakes fakes = new Fakes();

            public static IEnumerable<object[]> TrackMetricNames_Data
            {
                get
                {
                    var package = fakes.Package.Packages.First();
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

                    yield return new object[] { "ODataQueryFilter",
                        (TrackAction)(s => s.TrackODataQueryFilterEvent("callContext", true, true, "queryPattern"))
                    };

                    yield return new object[] { "PackagePush",
                        (TrackAction)(s => s.TrackPackagePushEvent(package, fakes.User, identity))
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

                    yield return new object[] { "PackageReflow",
                        (TrackAction)(s => s.TrackPackageReflow(package))
                    };

                    yield return new object[] { "PackageHardDeleteReflow",
                        (TrackAction)(s => s.TrackPackageHardDeleteReflow(fakes.Package.Id, package.Version))
                    };

                    yield return new object[] { "PackageRevalidate",
                        (TrackAction)(s => s.TrackPackageRevalidate(package))
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
                }
            }

            [Fact]
            public void TrackEventNamesIncludesAllEvents()
            {
                var expectedCount = typeof(TelemetryService.Events).GetFields().Length;
                var actualCount = TrackMetricNames_Data.Count();

                Assert.Equal(expectedCount, actualCount);
            }

            [Theory]
            [MemberData(nameof(TrackMetricNames_Data))]
            public void TrackMetricNames(string metricName, TrackAction track)
            {
                // Arrange
                var service = CreateService();

                // Act
                track(service);

                // Assert
                service.TelemetryClient.Verify(c => c.TrackMetric(metricName,
                    It.IsAny<double>(),
                    It.IsAny<IDictionary<string, string>>()),
                    Times.Once);
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
                        TraceEventType.Warning,
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()),
                    Times.Once);
                Assert.True(service.LastTraceMessage.Contains("InvalidOperationException"));
            }
        }

        public class BaseFacts
        {
            public class TelemetryServiceWrapper : TelemetryService
            {
                public TelemetryServiceWrapper(IDiagnosticsService diagnosticsService, ITelemetryClient telemetryClient)
                    : base(diagnosticsService, telemetryClient)
                {
                }

                public Mock<IDiagnosticsSource> TraceSource { get; set; }

                public Mock<ITelemetryClient> TelemetryClient { get; set; }

                public string LastTraceMessage { get; set; }
            }

            public TelemetryServiceWrapper CreateService()
            {
                var traceSource = new Mock<IDiagnosticsSource>();
                var traceService = new Mock<IDiagnosticsService>();
                var telemetryClient = new Mock<ITelemetryClient>();

                traceService.Setup(s => s.GetSource(It.IsAny<string>()))
                    .Returns(traceSource.Object);

                var telemetryService = new TelemetryServiceWrapper(traceService.Object, telemetryClient.Object);

                telemetryService.TraceSource = traceSource;
                telemetryService.TelemetryClient = telemetryClient;

                traceSource.Setup(t => t.TraceEvent(
                        It.IsAny<TraceEventType>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int>()))
                    .Callback<TraceEventType, int, string, string, string, int>(
                        (type, id, message, member, file, line) => telemetryService.LastTraceMessage = message)
                    .Verifiable();

                return telemetryService;
            }
        }
    }
}
