// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Autofac;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGet.Versioning;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Infrastructure.Mail.Messages;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    internal class TestableApiController
        : ApiController
    {
        public Mock<IApiScopeEvaluator> MockApiScopeEvaluator { get; private set; }
        public Mock<IEntitiesContext> MockEntitiesContext { get; private set; }
        public Mock<IPackageService> MockPackageService { get; private set; }
        public Mock<IPackageDeprecationManagementService> MockPackageDeprecationManagementService { get; private set; }
        public Mock<IPackageUpdateService> MockPackageUpdateService { get; private set; }
        public Mock<IPackageFileService> MockPackageFileService { get; private set; }
        public Mock<IUserService> MockUserService { get; private set; }
        public Mock<IContentService> MockContentService { get; private set; }
        public Mock<IStatisticsService> MockStatisticsService { get; private set; }
        public Mock<IIndexingService> MockIndexingService { get; private set; }
        public Mock<IMessageService> MockMessageService { get; private set; }
        public Mock<ITelemetryService> MockTelemetryService { get; private set; }
        public Mock<AuthenticationService> MockAuthenticationService { get; private set; }
        public Mock<ISecurityPolicyService> MockSecurityPolicyService { get; private set; }
        public Mock<IReservedNamespaceService> MockReservedNamespaceService { get; private set; }
        public Mock<IPackageUploadService> MockPackageUploadService { get; private set; }
        public Mock<IPackageDeleteService> MockPackageDeleteService { get; set; }
        public Mock<ISymbolPackageFileService> MockSymbolPackageFileService { get; set; }
        public Mock<ISymbolPackageUploadService> MockSymbolPackageUploadService { get; set; }

        private Stream PackageFromInputStream { get; set; }

        public TestableApiController(
            IGalleryConfigurationService configurationService,
            MockBehavior behavior = MockBehavior.Default,
            ISecurityPolicyService securityPolicyService = null,
            IUserService userService = null)
        {
            SetOwinContextOverride(Fakes.CreateOwinContext());
            ApiScopeEvaluator = (MockApiScopeEvaluator = new Mock<IApiScopeEvaluator>()).Object;
            EntitiesContext = (MockEntitiesContext = new Mock<IEntitiesContext>()).Object;
            PackageService = (MockPackageService = new Mock<IPackageService>(behavior)).Object;
            PackageDeprecationManagementService = (MockPackageDeprecationManagementService = new Mock<IPackageDeprecationManagementService>()).Object;
            PackageUpdateService = (MockPackageUpdateService = new Mock<IPackageUpdateService>()).Object;
            UserService = userService ?? (MockUserService = new Mock<IUserService>(behavior)).Object;
            ContentService = (MockContentService = new Mock<IContentService>()).Object;
            StatisticsService = (MockStatisticsService = new Mock<IStatisticsService>()).Object;
            IndexingService = (MockIndexingService = new Mock<IIndexingService>()).Object;
            AuthenticationService = (MockAuthenticationService = new Mock<AuthenticationService>()).Object;
            SecurityPolicyService = securityPolicyService ?? (MockSecurityPolicyService = new Mock<ISecurityPolicyService>()).Object;
            ReservedNamespaceService = (MockReservedNamespaceService = new Mock<IReservedNamespaceService>()).Object;
            SymbolPackageFileService = (MockSymbolPackageFileService = new Mock<ISymbolPackageFileService>()).Object;
            PackageUploadService = (MockPackageUploadService = new Mock<IPackageUploadService>()).Object;
            PackageDeleteService = (MockPackageDeleteService = new Mock<IPackageDeleteService>()).Object;
            SymbolPackageUploadService = (MockSymbolPackageUploadService = new Mock<ISymbolPackageUploadService>()).Object;

            SetupApiScopeEvaluatorOnAllInputs();

            CredentialBuilder = new CredentialBuilder();

            MockPackageFileService = new Mock<IPackageFileService>(MockBehavior.Strict);
            MockPackageFileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                .Returns(Task.CompletedTask);
            PackageFileService = MockPackageFileService.Object;

            MessageService = (MockMessageService = new Mock<IMessageService>()).Object;

            configurationService.Features.TrackPackageDownloadCountInLocalDatabase = false;
            ConfigurationService = configurationService;

            AuditingService = new TestAuditingService();

            MockTelemetryService = new Mock<ITelemetryService>();
            TelemetryService = MockTelemetryService.Object;

            if (MockSecurityPolicyService != null)
            {
                MockSecurityPolicyService.Setup(s => s.EvaluateUserPoliciesAsync(It.IsAny<SecurityPolicyAction>(), It.IsAny<User>(), It.IsAny<HttpContextBase>()))
                    .Returns(Task.FromResult(SecurityPolicyResult.SuccessResult));
                MockSecurityPolicyService.Setup(s => s.EvaluatePackagePoliciesAsync(It.IsAny<SecurityPolicyAction>(), It.IsAny<Package>(), It.IsAny<User>(), It.IsAny<User>(), It.IsAny<HttpContextBase>()))
                    .Returns(Task.FromResult(SecurityPolicyResult.SuccessResult));
            }

            MockReservedNamespaceService
                .Setup(s => s.GetReservedNamespacesForId(It.IsAny<string>()))
                .Returns(new ReservedNamespace[0]);

            MockPackageUploadService
                .Setup(x => x.ValidateBeforeGeneratePackageAsync(
                    It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), It.IsAny<User>()))
                .ReturnsAsync(PackageValidationResult.Accepted());

            MockPackageUploadService.Setup(x => x.GeneratePackageAsync(It.IsAny<string>(), It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<User>()))
                .Returns((string id, PackageArchiveReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User owner, User currentUser) =>
                {
                    var packageMetadata = PackageMetadata.FromNuspecReader(
                        nugetPackage.GetNuspecReader(),
                        strict: true);

                    var packageRegistration = new PackageRegistration { Id = packageMetadata.Id, IsVerified = false };
                    packageRegistration.Owners.Add(owner);

                    var package = new Package();
                    package.PackageRegistration = packageRegistration;
                    package.Version = packageMetadata.Version.ToString();
                    package.SemVerLevelKey = SemVerLevelKey.ForPackage(packageMetadata.Version, packageMetadata.GetDependencyGroups().AsPackageDependencyEnumerable());
                    package.FlattenedAuthors = packageMetadata.Authors.Flatten();
                    package.LicenseUrl = packageMetadata.LicenseUrl?.ToString();
                    package.ProjectUrl = packageMetadata.ProjectUrl?.ToString();
                    package.Copyright = packageMetadata.Copyright;

                    return Task.FromResult(package);
                });

            MockPackageUploadService
                .Setup(x => x.ValidateAfterGeneratePackageAsync(
                    It.IsAny<Package>(),
                    It.IsAny<PackageArchiveReader>(),
                    It.IsAny<User>(),
                    It.IsAny<User>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(PackageValidationResult.Accepted());

            MockSymbolPackageUploadService
                .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                .ReturnsAsync(SymbolPackageValidationResult.Accepted());

            var requestMock = new Mock<HttpRequestBase>();
            requestMock.Setup(m => m.IsSecureConnection).Returns(true);
            requestMock.Setup(m => m.Url).Returns(new Uri(TestUtility.GallerySiteRootHttps));

            var httpContextMock = new Mock<HttpContextBase>();
            httpContextMock.Setup(m => m.Request).Returns(requestMock.Object);

            TestUtility.SetupHttpContextMockForUrlGeneration(httpContextMock, this);
        }

        private void SetupApiScopeEvaluatorOnAllInputs()
        {
            MockApiScopeEvaluator
                .Setup(x => x.Evaluate(It.IsAny<User>(), It.IsAny<IEnumerable<Scope>>(), It.IsAny<IActionRequiringEntityPermissions<PackageRegistration>>(), It.IsAny<PackageRegistration>(), It.IsAny<string[]>()))
                .Returns(() => new ApiScopeEvaluationResult(GetCurrentUser(), PermissionsCheckResult.Allowed, scopesAreValid: true));

            MockApiScopeEvaluator
                .Setup(x => x.Evaluate(It.IsAny<User>(), It.IsAny<IEnumerable<Scope>>(), It.IsAny<IActionRequiringEntityPermissions<ActionOnNewPackageContext>>(), It.IsAny<ActionOnNewPackageContext>(), It.IsAny<string[]>()))
                .Returns(() => new ApiScopeEvaluationResult(GetCurrentUser(), PermissionsCheckResult.Allowed, scopesAreValid: true));
        }

        internal void SetupPackageFromInputStream(Stream packageStream)
        {
            PackageFromInputStream = packageStream;
        }

        protected internal override Stream ReadPackageFromRequest()
        {
            return PackageFromInputStream;
        }
    }

    public class ApiControllerFacts
    {
        private static readonly Uri HttpRequestUrl = new Uri("http://nuget.org/api/v2/something");
        private static readonly Uri HttpsRequestUrl = new Uri("https://nuget.org/api/v2/something");

        public static IEnumerable<object[]> InvalidScopes_Data
        {
            get
            {
                yield return MemberDataHelper.AsData(new ApiScopeEvaluationResult(null, PermissionsCheckResult.Unknown, scopesAreValid: false), HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);

                foreach (var result in Enum.GetValues(typeof(PermissionsCheckResult)).Cast<PermissionsCheckResult>())
                {
                    if (result == PermissionsCheckResult.Allowed)
                    {
                        yield return MemberDataHelper.AsData(
                            new ApiScopeEvaluationResult(new User("testOwner") { Key = 94443 }, result, scopesAreValid: true),
                            HttpStatusCode.Forbidden,
                            Strings.ApiKeyOwnerUnconfirmed);
                    }

                    if (result == PermissionsCheckResult.Allowed || result == PermissionsCheckResult.Unknown)
                    {
                        continue;
                    }

                    var isReservedNamespaceConflict = result == PermissionsCheckResult.ReservedNamespaceFailure;
                    var statusCode = isReservedNamespaceConflict ? HttpStatusCode.Conflict : HttpStatusCode.Forbidden;
                    var description = isReservedNamespaceConflict ? Strings.UploadPackage_IdNamespaceConflict : Strings.ApiKeyNotAuthorized;
                    yield return MemberDataHelper.AsData(new ApiScopeEvaluationResult(null, result, scopesAreValid: true), statusCode, description);
                }
            }
        }

        public class TheCreateSymbolPackageAction : TestContainer
        {
            [Fact]
            public async Task CreateSymbolPackage_ReturnsFailedActionWhenValidationFails()
            {
                // Arrange
                var controller = new TestableApiController(GetConfigurationService());
                controller
                    .MockSymbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(SymbolPackageValidationResult.Invalid("Invalid symbol package"));

                var user = new User("test") { Key = 1 };
                controller.SetCurrentUser(user);

                // Act
                var result = await controller.CreateSymbolPackagePutAsync();

                // Assert
                Assert.NotNull(result);
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest);
            }

            [Fact]
            public async Task CreateSymbolPackage_UnauthorizedUserWillGet403()
            {
                // Arrange	
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                var packageId = "theId";
                var version = "1.0.42";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId
                    },
                    Version = version
                };
                controller
                    .MockSymbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(SymbolPackageValidationResult.AcceptedForPackage(package));

                var owner = new User("owner") { Key = 2, EmailAddress = "org@confirmed.com" };
                Expression<Func<IApiScopeEvaluator, ApiScopeEvaluationResult>> evaluateApiScope =
                    x => x.Evaluate(
                        user,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.UploadSymbolPackage,
                        It.IsAny<PackageRegistration>(),
                        NuGetScopes.PackagePushVersion,
                        NuGetScopes.PackagePush);
                controller.MockApiScopeEvaluator
                   .Setup(evaluateApiScope)
                   .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.AccountFailure, scopesAreValid: false));

                // Act
                ActionResult result = await controller.CreateSymbolPackagePutAsync();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.Unauthorized);
                controller.AuditingService.WroteRecord<FailedAuthenticatedOperationAuditRecord>(
                   (record) =>
                   {
                       return
                           record.UsernameOrEmail == user.Username &&
                           record.Action == AuditedAuthenticatedOperationAction.SymbolsPackagePushAttemptByNonOwner &&
                           record.AttemptedPackage.Id == packageId &&
                           record.AttemptedPackage.Version == version;
                   });
            }

            [Fact]
            public async Task CreateSymbolPackage_ReturnsConflictWhenUploadingDuplicateSymbolsPackage()
            {
                // Arrange
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                var packageId = "theId";
                var version = "1.0.42";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId
                    },
                    Version = version
                };

                controller
                    .MockSymbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(SymbolPackageValidationResult.AcceptedForPackage(package));
                controller
                    .MockSymbolPackageUploadService
                    .Setup(x => x.CreateAndUploadSymbolsPackage(package, It.IsAny<Stream>()))
                    .ReturnsAsync(PackageCommitResult.Conflict);

                // Act
                ActionResult result = await controller.CreateSymbolPackagePutAsync();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.Conflict);
            }

            [Fact]
            public async Task CreateSymbolPackage_CreatesSymbolPackageAndWritesAudit()
            {
                // Arrange
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                var packageId = "theId";
                var version = "1.0.42";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = packageId
                    },
                    Version = version
                };

                controller
                    .MockSymbolPackageUploadService
                    .Setup(x => x.ValidateUploadedSymbolsPackage(It.IsAny<Stream>(), It.IsAny<User>()))
                    .ReturnsAsync(SymbolPackageValidationResult.AcceptedForPackage(package));
                controller
                    .MockSymbolPackageUploadService
                    .Setup(x => x.CreateAndUploadSymbolsPackage(package, It.IsAny<Stream>()))
                    .ReturnsAsync(PackageCommitResult.Success);

                // Act
                ActionResult result = await controller.CreateSymbolPackagePutAsync();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.Created);
                controller.AuditingService.WroteRecord<PackageAuditRecord>(
                   (record) =>
                   {
                       return
                           record.Action == AuditedPackageAction.SymbolsCreate &&
                           record.RegistrationRecord.Id == packageId &&
                           record.PackageRecord.Version == version;
                   });
            }

            [Fact]
            public async Task CreateSymbolPackage_WillTraceFailureToPushEvent()
            {
                // Arrange
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);

                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "TheId"
                    },
                    Version = "1.0.42",
                    SymbolPackages = new HashSet<SymbolPackage>()
                };

                controller.MockSymbolPackageUploadService
                   .Setup(x => x.ValidateUploadedSymbolsPackage(
                       It.IsAny<Stream>(), It.IsAny<User>()))
                   .ThrowsAsync(new SymbolsTestException("Test exception."));

                // Act
                var exception = await Assert.ThrowsAsync<SymbolsTestException>(() => controller.CreateSymbolPackagePutAsync());

                // Assert
                controller.MockTelemetryService.Verify(
                   x => x.TrackSymbolPackagePushFailureEvent(It.IsAny<string>(), It.IsAny<string>()),
                   Times.Once());
            }

            [Fact]
            public async Task CreateSymbolPackage_WillReturnBadRequestForInvalidPackage()
            {
                // Arrange
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);

                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "TheId"
                    },
                    Version = "1.0.42",
                    SymbolPackages = new HashSet<SymbolPackage>()
                };

                controller.MockSymbolPackageUploadService
                   .Setup(x => x.ValidateUploadedSymbolsPackage(
                       It.IsAny<Stream>(), It.IsAny<User>()))
                   .ThrowsAsync(new InvalidPackageException("Test exception."));

                // Act
                var result = await controller.CreateSymbolPackagePutAsync();

                // Assert
                Assert.NotNull(result);
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest);
            }
        }

        public class TheCreatePackageAction
            : TestContainer
        {
            [Fact]
            public async Task CreatePackage_TracksFailureIfUnexpectedExceptionWithoutIdVersion()
            {
                // Arrange
                var controller = new TestableApiController(GetConfigurationService());
                controller.MockSecurityPolicyService
                    .Setup(x => x.EvaluateUserPoliciesAsync(It.IsAny<SecurityPolicyAction>(), It.IsAny<User>(), It.IsAny<HttpContextBase>()))
                    .Throws<Exception>();
                var user = new User("test") { Key = 1 };
                controller.SetCurrentUser(user);

                // Act
                await Assert.ThrowsAnyAsync<Exception>(() => controller.CreatePackagePut());

                // Assert
                controller.MockTelemetryService.Verify(x => x.TrackPackagePushFailureEvent(null, null), Times.Once());
            }

            [Fact]
            public async Task CreatePackage_TracksFailureIfUnexpectedExceptionWithIdVersion()
            {
                // Arrange
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = "theId";
                packageRegistration.Owners.Add(user);
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.42";
                packageRegistration.Packages.Add(package);

                TestGalleryConfigurationService configurationService = GetConfigurationService();
                var controller = new TestableApiController(configurationService);
                controller.SetCurrentUser(user);
                controller.MockPackageUploadService
                    .Setup(p => p.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>(),
                        It.IsAny<User>()))
                    .Returns(Task.FromResult(package));
                controller.MockPackageService
                    .Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                    .Throws<Exception>();

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                await Assert.ThrowsAnyAsync<Exception>(() => controller.CreatePackagePut());

                // Assert
                controller.MockTelemetryService.Verify(x => x.TrackPackagePushFailureEvent(packageRegistration.Id, new NuGetVersion(package.Version)), Times.Once());
            }

            [Fact]
            public async Task CreatePackage_Returns400IfSecurityPolicyFails()
            {
                // Arrange
                var controller = new TestableApiController(GetConfigurationService());
                controller.MockSecurityPolicyService.Setup(s => s.EvaluateUserPoliciesAsync(It.IsAny<SecurityPolicyAction>(), It.IsAny<User>(), It.IsAny<HttpContextBase>()))
                    .Returns(Task.FromResult(SecurityPolicyResult.CreateErrorResult("A")));

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest, "A");
            }

            [Fact]
            public async Task CreatePackage_Returns400IfPackageSecurityPolicyFails()
            {
                // Arrange
                var packageId = "theId";
                var packageRegistration = new PackageRegistration { Id = packageId };
                packageRegistration.Id = packageId;
                var package = new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "1.0.42"
                };
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController(GetConfigurationService());

                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(packageRegistration);

                var fakes = Get<Fakes>();
                var currentUser = fakes.User;
                controller.SetCurrentUser(currentUser);

                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, "1.0.42");
                controller.SetupPackageFromInputStream(nuGetPackage);

                var owner = new User("owner") { Key = 2, EmailAddress = "org@confirmed.com" };

                Expression<Func<IApiScopeEvaluator, ApiScopeEvaluationResult>> evaluateApiScope =
                    x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.UploadNewPackageVersion,
                        packageRegistration,
                        NuGetScopes.PackagePushVersion, NuGetScopes.PackagePush);

                controller.MockApiScopeEvaluator
                    .Setup(evaluateApiScope)
                    .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));

                controller.MockSecurityPolicyService.Setup(s => s.EvaluatePackagePoliciesAsync(It.IsAny<SecurityPolicyAction>(), It.IsAny<Package>(), currentUser, owner, It.IsAny<HttpContextBase>()))
                    .Returns(Task.FromResult(SecurityPolicyResult.CreateErrorResult("Package not compliant.\n\rFix your package!")));

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest, "Package not compliant. Fix your package!");
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange
                var user = new User { EmailAddress = "confirmed@email.com" };
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = "theId";
                packageRegistration.Owners.Add(user);
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.42";
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.MockPackageUploadService
                    .Setup(p => p.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>(),
                        It.IsAny<User>()))
                    .Returns(Task.FromResult(package));

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                await controller.CreatePackagePut();

                // Assert
                Assert.True(controller.AuditingService.WroteRecord<PackageAuditRecord>(ar =>
                    ar.Action == AuditedPackageAction.Create
                    && ar.Id == package.PackageRegistration.Id
                    && ar.Version == package.Version));
            }

            [Theory]
            [InlineData(false, false, true)]
            [InlineData(true, false, true)]
            [InlineData(false, true, true)]
            [InlineData(true, true, false)]
            public async Task CreatePackageWillSendPackageAddedNotice(bool asyncValidationEnabled, bool blockingValidationEnabled, bool callExpected)
            {
                // Arrange
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = "theId";
                packageRegistration.Owners.Add(user);
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.42";
                packageRegistration.Packages.Add(package);

                TestGalleryConfigurationService configurationService = GetConfigurationService();
                configurationService.Current.AsynchronousPackageValidationEnabled = asyncValidationEnabled;
                configurationService.Current.BlockingAsynchronousPackageValidationEnabled = blockingValidationEnabled;
                var controller = new TestableApiController(configurationService);
                controller.SetCurrentUser(user);
                controller.MockPackageUploadService
                    .Setup(p => p.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>(),
                        It.IsAny<User>()))
                    .Returns(Task.FromResult(package));

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                await controller.CreatePackagePut();

                // Assert
                controller.MockMessageService
                    .Verify(ms => ms.SendMessageAsync(
                        It.Is<PackageAddedMessage>(msg => msg.Package == package),
                        false,
                        false),
                    Times.Exactly(callExpected ? 1 : 0));
            }

            [Fact]
            public async Task CreatePackageWillReturn400IfFileIsNotANuGetPackageInternal()
            {
                // Arrange
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var packageRegistration = new PackageRegistration();
                packageRegistration.Owners.Add(user);

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(packageRegistration);

                byte[] data = new byte[100];
                controller.SetupPackageFromInputStream(new MemoryStream(data));

                // Act
                ActionResult result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest);
            }

            private const string EnsureValidExceptionMessage = "naughty package";

            [Theory]
            [InlineData(typeof(InvalidPackageException), true)]
            [InlineData(typeof(InvalidDataException), true)]
            [InlineData(typeof(EntityException), true)]
            [InlineData(typeof(Exception), false)]
            public async Task CreatePackageReturns400IfEnsureValidThrowsExceptionMessage(Type exceptionType, bool expectExceptionMessageInResponse)
            {
                // Arrange
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                var exception =
                    exceptionType.GetConstructor(new[] { typeof(string) }).Invoke(new[] { EnsureValidExceptionMessage });

                controller.MockPackageService.Setup(p => p.EnsureValid(It.IsAny<PackageArchiveReader>()))
                    .Throws(exception as Exception);

                // Act
                ActionResult result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest);
                Assert.Equal(expectExceptionMessageInResponse ? EnsureValidExceptionMessage : Strings.FailedToReadUploadFile, (result as HttpStatusCodeWithBodyResult).StatusDescription);
            }

            [Fact]
            public async Task CreatePackageReturns400IfMinClientVersionIsTooHigh()
            {
                // Arrange
                const string HighClientVerison = "6.0.0.0";
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42", minClientVersion: HighClientVerison);

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                ActionResult result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest);
                Assert.Contains(HighClientVerison, (result as HttpStatusCodeWithBodyResult).StatusDescription);
            }

            [Theory]
            [InlineData("ILike*Asterisks")]
            [InlineData("I_.Like.-Separators")]
            [InlineData("-StartWithSeparator")]
            [InlineData("EndWithSeparator.")]
            [InlineData("EndsWithHyphen-")]
            [InlineData("$id$")]
            [InlineData("Contains#Invalid$Characters!@#$%^&*")]
            [InlineData("Contains#Invalid$Characters!@#$%^&*EndsOnValidCharacter")]
            public async Task CreatePackageReturns400IfPackageIdIsInvalid(string packageId)
            {
                // Arrange
                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                ActionResult result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest);
            }

            [Theory]
            [InlineData(PackageStatus.Available)]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(PackageStatus.Validating)]
            public async Task WillReturnConflictIfAPackageWithTheIdAndSameNormalizedVersionAlreadyExists(PackageStatus status)
            {
                var id = "theId";
                var version = "1.0.42";
                var nuGetPackage = TestPackage.CreateTestPackageStream(id, version);

                var user = new User() { EmailAddress = "confirmed1@email.com" };

                var conflictingPackage = new Package { Version = version, NormalizedVersion = version, PackageStatusKey = status };
                var packageRegistration = new PackageRegistration
                {
                    Id = id,
                    Packages = new List<Package> { conflictingPackage },
                    Owners = new List<User> { user }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(new User() { EmailAddress = "confirmed2@email.com" });
                controller.MockPackageService.Setup(x => x.FindPackageRegistrationById(id)).Returns(packageRegistration);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(id, version)).Returns(conflictingPackage);
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                controller.MockPackageDeleteService.Verify(
                    x => x.HardDeletePackagesAsync(
                        It.IsAny<IEnumerable<Package>>(),
                        It.IsAny<User>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<bool>()),
                    Times.Never());

                controller.MockTelemetryService.Verify(
                    x => x.TrackPackageReupload(It.IsAny<Package>()),
                    Times.Never());

                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Conflict,
                    String.Format(Strings.PackageExistsAndCannotBeModified, id, version));
            }

            [Fact]
            public async Task WillAllowReuploadingPackageIfFailedValidation()
            {
                var id = "theId";
                var version = "1.0.42";
                var nuGetPackage = TestPackage.CreateTestPackageStream(id, version);

                var user = new User() { EmailAddress = "confirmed1@email.com" };

                var conflictingPackage = new Package { Version = version, NormalizedVersion = version, PackageStatusKey = PackageStatus.FailedValidation };
                var packageRegistration = new PackageRegistration
                {
                    Id = id,
                    Packages = new List<Package> { conflictingPackage },
                    Owners = new List<User> { user }
                };

                var currentUser = new User() { EmailAddress = "confirmed2@email.com" };

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(currentUser);
                controller.MockPackageService.Setup(x => x.FindPackageRegistrationById(id)).Returns(packageRegistration);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(id, version)).Returns(conflictingPackage);
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                controller.MockPackageDeleteService.Verify(
                    x => x.HardDeletePackagesAsync(
                        new[] { conflictingPackage },
                        currentUser,
                        Strings.FailedValidationHardDeleteReason,
                        Strings.AutomatedPackageDeleteSignature,
                        false),
                    Times.Once());

                controller.MockTelemetryService.Verify(
                    x => x.TrackPackageReupload(conflictingPackage),
                    Times.Once());

                controller.MockPackageUploadService.Verify(
                    x => x.CommitPackageAsync(
                        It.IsAny<Package>(),
                        It.IsAny<Stream>()),
                    Times.Once);
            }

            [Fact]
            public async Task WillReturnConflictIfCommittingPackageReturnsConflict()
            {
                // Arrange
                var user = new User { EmailAddress = "confirmed1@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller
                    .MockPackageUploadService
                    .Setup(x => x.CommitPackageAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .ReturnsAsync(PackageCommitResult.Conflict);

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                controller.SetCurrentUser(new User() { EmailAddress = "confirmed2@email.com" });
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Conflict,
                    Strings.UploadPackage_IdVersionConflict);

                controller.MockEntitiesContext.VerifyCommitted(Times.Never());
            }

            [Fact]
            public async Task WillReturnConflictIfGeneratePackageThrowsPackageAlreadyExistsException()
            {
                // Arrange
                var packageId = "theId";
                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, "1.0.42");

                var currentUser = new User("currentUser") { Key = 1, EmailAddress = "currentUser@confirmed.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(currentUser);
                controller.SetupPackageFromInputStream(nuGetPackage);

                var owner = new User("owner") { Key = 2, EmailAddress = "org@confirmed.com" };

                Expression<Func<IApiScopeEvaluator, ApiScopeEvaluationResult>> evaluateApiScope =
                    x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.UploadNewPackageId,
                        It.Is<ActionOnNewPackageContext>((context) => context.PackageId == packageId),
                        NuGetScopes.PackagePush);

                controller.MockApiScopeEvaluator
                    .Setup(evaluateApiScope)
                    .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));
                controller
                    .MockPackageUploadService
                    .Setup(x => x.GeneratePackageAsync(It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>(),
                        It.IsAny<User>()))
                    .Throws(new PackageAlreadyExistsException("Package exists"));

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.Conflict);
                controller.MockPackageUploadService.Verify(x => x.GeneratePackageAsync(It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>(),
                        It.IsAny<User>()), Times.Once);
            }

            [Fact]
            public async Task WillReturnValidationWarnings()
            {
                // Arrange
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var messageA = "Warning A";
                var messageB = "Warning B";

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);
                controller
                    .MockPackageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(
                        It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), It.IsAny<User>()))
                    .ReturnsAsync(PackageValidationResult.AcceptedWithWarnings(new[] { new PlainTextOnlyValidationMessage(messageA) }));
                controller
                    .MockPackageUploadService
                    .Setup(x => x.ValidateAfterGeneratePackageAsync(
                        It.IsAny<Package>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<User>(),
                        It.IsAny<User>(),
                        It.IsAny<bool>()))
                    .ReturnsAsync(PackageValidationResult.AcceptedWithWarnings(new[] { new PlainTextOnlyValidationMessage(messageB) }));

                // Act
                ActionResult result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.Created);
                var warningResult = Assert.IsAssignableFrom<HttpStatusCodeWithServerWarningResult>(result);
                Assert.Equal(2, warningResult.Warnings.Count);
                Assert.Equal(messageA, warningResult.Warnings[0]);
                Assert.Equal(messageB, warningResult.Warnings[1]);
            }

            [Theory]
            [InlineData(PackageValidationResultType.Invalid)]
            public async Task WillReturnValidationMessageWhenValidationFails(PackageValidationResultType type)
            {
                // Arrange
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                var user = new User() { EmailAddress = "confirmed@email.com" };
                var message = "The package is just bad.";

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);
                controller
                    .MockPackageUploadService
                    .Setup(x => x.ValidateAfterGeneratePackageAsync(
                        It.IsAny<Package>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<User>(),
                        It.IsAny<User>(),
                        It.IsAny<bool>()))
                    .ReturnsAsync(new PackageValidationResult(type, message));

                // Act
                ActionResult result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest, message);
            }

            public static IEnumerable<object[]> PackageValidationResultTypes => Enum
                .GetValues(typeof(PackageValidationResultType))
                .Cast<PackageValidationResultType>()
                .Select(t => new object[] { t });

            [Theory]
            [MemberData(nameof(PackageValidationResultTypes))]
            public async Task DoesNotThrowForAnyPackageValidationResultType(PackageValidationResultType type)
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);
                controller.MockPackageUploadService
                    .Setup(x => x.ValidateBeforeGeneratePackageAsync(
                        It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), It.IsAny<User>()))
                    .ReturnsAsync(new PackageValidationResult(type, string.Empty));

                await controller.CreatePackagePut();

                controller.MockPackageUploadService.Verify(
                    x => x.ValidateBeforeGeneratePackageAsync(
                        It.IsAny<PackageArchiveReader>(), It.IsAny<PackageMetadata>(), It.IsAny<User>()),
                    Times.Once);
            }

            public static IEnumerable<object[]> CommitResults => Enum
                .GetValues(typeof(PackageCommitResult))
                .Cast<PackageCommitResult>()
                .Select(r => new object[] { r });

            [Theory]
            [MemberData(nameof(CommitResults))]
            public async Task DoesNotThrowForAnyPackageCommitResult(PackageCommitResult commitResult)
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);
                controller.MockPackageUploadService
                    .Setup(x => x.CommitPackageAsync(
                        It.IsAny<Package>(),
                        It.IsAny<Stream>()))
                    .ReturnsAsync(commitResult);

                await controller.CreatePackagePut();

                controller.MockPackageUploadService.Verify(
                    x => x.CommitPackageAsync(
                        It.IsAny<Package>(),
                        It.IsAny<Stream>()),
                    Times.Once);
            }

            [Fact]
            public async Task WillCreateAPackageWithNewRegistration()
            {
                var packageId = "theId";
                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, "1.0.42", minClientVersion: "1.0.0");

                var currentUser = new User("currentUser") { Key = 1, EmailAddress = "currentUser@confirmed.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(currentUser);
                controller.SetupPackageFromInputStream(nuGetPackage);

                var owner = new User("owner") { Key = 2, EmailAddress = "org@confirmed.com" };

                Expression<Func<IApiScopeEvaluator, ApiScopeEvaluationResult>> evaluateApiScope =
                    x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.UploadNewPackageId,
                        It.Is<ActionOnNewPackageContext>((context) => context.PackageId == packageId),
                        NuGetScopes.PackagePush);

                controller.MockApiScopeEvaluator
                    .Setup(evaluateApiScope)
                    .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));

                await controller.CreatePackagePut();

                controller.MockPackageUploadService.Verify(
                    x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        owner,
                        currentUser),
                    Times.Once);

                controller.MockApiScopeEvaluator.Verify(evaluateApiScope);
            }

            /// <remarks>
            /// <see cref="ApiController.CreatePackagePut"/> returns <see cref="HttpStatusCode.Unauthorized"/> instead of <see cref="HttpStatusCode.Forbidden"/>.
            /// </remarks>
            public static IEnumerable<object[]> WillNotCreateAPackageIfScopesInvalid_Data =>
                InvalidScopes_Data
                    .Select(x => x
                        .Select(y => y is HttpStatusCode status && status == HttpStatusCode.Forbidden ? HttpStatusCode.Unauthorized : y)
                        .ToArray());

            [Theory]
            [MemberData(nameof(WillNotCreateAPackageIfScopesInvalid_Data))]
            public async Task WillNotCreateAPackageIfScopesInvalidWithNewRegistration(ApiScopeEvaluationResult scopeEvaluationResult, HttpStatusCode expectedStatusCode, string description)
            {
                // Arrange
                var controller = new TestableApiController(GetConfigurationService());

                var fakes = Get<Fakes>();
                var currentUser = fakes.User;
                controller.SetCurrentUser(currentUser);

                var packageId = "theId";
                var packageVersion = "1.0.42";
                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, packageVersion);
                controller.SetupPackageFromInputStream(nuGetPackage);

                controller.MockApiScopeEvaluator
                    .Setup(x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.UploadNewPackageId,
                        It.Is<ActionOnNewPackageContext>((context) => context.PackageId == packageId),
                        NuGetScopes.PackagePush))
                    .Returns(scopeEvaluationResult);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    expectedStatusCode,
                    description);

                controller.AuditingService.WroteRecord<FailedAuthenticatedOperationAuditRecord>(
                    (record) =>
                    {
                        return
                            record.UsernameOrEmail == currentUser.Username &&
                            record.Action == AuditedAuthenticatedOperationAction.PackagePushAttemptByNonOwner &&
                            record.AttemptedPackage.Id == packageId &&
                            record.AttemptedPackage.Version == packageVersion;
                    });

                controller.MockPackageUploadService.Verify(
                    x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>(),
                        It.IsAny<User>()),
                    Times.Never);
            }

            [Fact]
            public async Task WillCreateAPackageWithExistingRegistration()
            {
                var packageId = "theId";
                var packageRegistration = new PackageRegistration { Id = packageId };
                packageRegistration.Id = packageId;
                var package = new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = "1.0.42"
                };
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(packageRegistration);

                var fakes = Get<Fakes>();
                var currentUser = fakes.User;
                controller.SetCurrentUser(currentUser);

                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, "1.0.42");
                controller.SetupPackageFromInputStream(nuGetPackage);

                var owner = new User("owner") { Key = 2, EmailAddress = "org@confirmed.com" };

                Expression<Func<IApiScopeEvaluator, ApiScopeEvaluationResult>> evaluateApiScope =
                    x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.UploadNewPackageVersion,
                        packageRegistration,
                        NuGetScopes.PackagePushVersion, NuGetScopes.PackagePush);

                controller.MockApiScopeEvaluator
                    .Setup(evaluateApiScope)
                    .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, scopesAreValid: true));

                await controller.CreatePackagePut();

                controller.MockPackageUploadService.Verify(
                    x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        owner,
                        currentUser),
                    Times.Once);

                controller.MockApiScopeEvaluator.Verify(evaluateApiScope);
            }

            [Theory]
            [MemberData(nameof(WillNotCreateAPackageIfScopesInvalid_Data))]
            public async Task WillNotCreateAPackageIfScopesInvalidWithExistingRegistration(ApiScopeEvaluationResult scopeEvaluationResult, HttpStatusCode expectedStatusCode, string description)
            {
                // Arrange
                var packageId = "theId";
                var packageVersion = "1.0.42";
                var packageRegistration = new PackageRegistration { Id = packageId };
                packageRegistration.Id = packageId;
                var package = new Package
                {
                    PackageRegistration = packageRegistration,
                    Version = packageVersion
                };
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(packageRegistration);

                var fakes = Get<Fakes>();
                var currentUser = fakes.User;
                controller.SetCurrentUser(currentUser);

                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, packageVersion);
                controller.SetupPackageFromInputStream(nuGetPackage);

                controller.MockApiScopeEvaluator
                    .Setup(x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.UploadNewPackageVersion,
                        packageRegistration,
                        NuGetScopes.PackagePushVersion, NuGetScopes.PackagePush))
                    .Returns(scopeEvaluationResult);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    expectedStatusCode,
                    description);

                controller.MockPackageUploadService.Verify(
                    x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>(),
                        It.IsAny<User>()),
                    Times.Never);
            }

            [Fact]
            public async Task WillReturnServerWarningWhenCreatingSemVer2Package()
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42+metadata");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                var actionResult = await controller.CreatePackagePut();

                Assert.IsType<HttpStatusCodeWithServerWarningResult>(actionResult);
            }

            [Fact]
            public async Task WillCreateAPackageWithTheUserMatchingTheApiKey()
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                await controller.CreatePackagePut();

                controller.MockPackageUploadService.Verify(
                    x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        user,
                        user),
                    Times.Once);
            }

            [Fact]
            public async Task WillSendPackagePushEvent()
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                await controller.CreatePackagePut();

                controller.MockTelemetryService.Verify(x => x.TrackPackagePushEvent(It.IsAny<Package>(), user, controller.OwinContext.Request.User.Identity), Times.Once);
            }

            [Fact]
            public async Task WillFailIfPackageRegistrationIsLocked()
            {
                // Arrange
                const string PackageId = "theId";
                var nuGetPackage = TestPackage.CreateTestPackageStream(PackageId, "1.0.42");

                var user = new User() { EmailAddress = "confirmed1@email.com" };
                var packageRegistration = new PackageRegistration
                {
                    Id = PackageId,
                    IsLocked = true,
                    Owners = new List<User> { user }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(new User() { EmailAddress = "confirmed2@email.com" });
                controller.MockPackageService.Setup(x => x.FindPackageRegistrationById(PackageId)).Returns(packageRegistration);
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                var statusCodeResult = result as HttpStatusCodeWithBodyResult;

                Assert.NotNull(statusCodeResult);
                Assert.Equal((int)HttpStatusCode.Forbidden, statusCodeResult.StatusCode);
                Assert.Contains(PackageId, statusCodeResult.StatusDescription);
            }

            public class CalledByUserWithMicrosoftTeamSubscription
                : TestContainer
            {
                private const string _packageId = "theId";
                private static readonly User _requiredCoOwner = new User
                {
                    Username = MicrosoftTeamSubscription.MicrosoftUsername
                };

                private readonly User _user;
                private readonly Mock<IUserService> _userServiceMock;

                public CalledByUserWithMicrosoftTeamSubscription()
                {
                    // Arrange
                    var microsoftTeamSubscription = new MicrosoftTeamSubscription();
                    _user = new User()
                    {
                        EmailAddress = "confirmed@email.com",
                        Username = "theUser",
                        SecurityPolicies = microsoftTeamSubscription.Policies.ToList()
                    };

                    _userServiceMock = new Mock<IUserService>(MockBehavior.Strict);
                    _userServiceMock.Setup(m => m.FindByUsername(MicrosoftTeamSubscription.MicrosoftUsername, false))
                        .Returns(_requiredCoOwner)
                        .Verifiable();
                }

                public static IEnumerable<object[]> NonCompliantPackages_Data
                {
                    get
                    {
                        var packageId = "theId";
                        var microsoftTeamSubscription = new MicrosoftTeamSubscription();
                        var user = new User()
                        {
                            EmailAddress = "confirmed@email.com",
                            Username = "theUser",
                            SecurityPolicies = microsoftTeamSubscription.Policies.ToList()
                        };

                        // Missing required co-owner
                        yield return MemberDataHelper.AsData(
                            CreatePackage(
                                packageId,
                                "1.0.0",
                                isSigned: false,
                                authors: $"{user.Username}",
                                licenseUrl: new Uri("https://github.com/NuGet/NuGetGallery/blob/master/LICENSE.txt"),
                                projectUrl: new Uri("https://www.nuget.org")).Object,
                            user);

                        // Missing license url
                        yield return MemberDataHelper.AsData(
                            CreatePackage(
                                packageId,
                                "1.0.0",
                                isSigned: false,
                                authors: $"{user.Username},{_requiredCoOwner.Username}",
                                licenseUrl: null,
                                projectUrl: new Uri("https://www.nuget.org")).Object,
                            user);

                        // Missing project url
                        yield return MemberDataHelper.AsData(
                            CreatePackage(
                                packageId,
                                "1.0.0",
                                isSigned: false,
                                authors: $"{user.Username},{_requiredCoOwner.Username}",
                                licenseUrl: new Uri("https://github.com/NuGet/NuGetGallery/blob/master/LICENSE.txt"),
                                projectUrl: null).Object,
                            user);
                    }
                }

                [Fact]
                public async Task AddsRequiredCoOwnerWhenPackageWithNewRegistrationIdIsCompliant()
                {
                    var nuGetPackageMock = CreatePackage(
                        _packageId,
                        "1.0.0",
                        isSigned: true,
                        authors: $"{_requiredCoOwner.Username}",
                        licenseUrl: new Uri("https://github.com/NuGet/NuGetGallery/blob/master/LICENSE.txt"),
                        projectUrl: new Uri("https://www.nuget.org"));

                    var packageOwnershipManagementServiceMock = new Mock<IPackageOwnershipManagementService>(MockBehavior.Strict);
                    packageOwnershipManagementServiceMock
                        .Setup(m => m.AddPackageOwnerAsync(It.IsAny<PackageRegistration>(), _requiredCoOwner, false /* not committing changes! */))
                        .Returns(Task.CompletedTask)
                        .Verifiable();

                    var securityPolicyService = CreateSecurityPolicyService(
                        new Lazy<IUserService>(() => _userServiceMock.Object),
                        new Lazy<IPackageOwnershipManagementService>(() => packageOwnershipManagementServiceMock.Object));

                    var controller = new TestableApiController(
                        GetConfigurationService(),
                        MockBehavior.Strict,
                        securityPolicyService,
                        _userServiceMock.Object);

                    controller.SetCurrentUser(_user);
                    controller.SetupPackageFromInputStream(nuGetPackageMock.Object.GetStream());
                    controller.MockPackageService
                        .Setup(m => m.FindPackageRegistrationById(_packageId))
                        .Returns((PackageRegistration)null)
                        .Verifiable();
                    controller.MockPackageService
                        .Setup(m => m.EnsureValid(It.IsAny<PackageArchiveReader>()))
                        .Returns(Task.FromResult(true))
                        .Verifiable();

                    // Act
                    var result = await controller.CreatePackagePut();

                    // Assert
                    var statusCodeResult = result as HttpStatusCodeWithServerWarningResult;

                    Assert.NotNull(statusCodeResult);
                    Assert.Equal((int)HttpStatusCode.Created, statusCodeResult.StatusCode);

                    _userServiceMock.VerifyAll();
                    packageOwnershipManagementServiceMock.VerifyAll();
                    controller.MockPackageService.VerifyAll();
                }

                [Theory]
                [MemberData(nameof(NonCompliantPackages_Data))]
                public async Task DoesNotAddRequiredCoOwnerWhenPackageIsNotCompliant(TestPackageReader packageReader, User user)
                {
                    // Arrange
                    var packageOwnershipManagementServiceMock = new Mock<IPackageOwnershipManagementService>(MockBehavior.Strict);

                    var securityPolicyService = CreateSecurityPolicyService(
                        new Lazy<IUserService>(() => _userServiceMock.Object),
                        new Lazy<IPackageOwnershipManagementService>(() => packageOwnershipManagementServiceMock.Object));

                    var controller = new TestableApiController(
                        GetConfigurationService(),
                        MockBehavior.Strict,
                        securityPolicyService,
                        _userServiceMock.Object);

                    controller.SetCurrentUser(user);
                    controller.SetupPackageFromInputStream(packageReader.GetStream());
                    controller.MockPackageService
                        .Setup(m => m.FindPackageRegistrationById(_packageId))
                        .Returns((PackageRegistration)null)
                        .Verifiable();
                    controller.MockPackageService
                        .Setup(m => m.EnsureValid(It.IsAny<PackageArchiveReader>()))
                        .Returns(Task.FromResult(true))
                        .Verifiable();

                    // Act
                    var result = await controller.CreatePackagePut();

                    // Assert
                    var statusCodeResult = result as HttpStatusCodeWithBodyResult;

                    Assert.NotNull(statusCodeResult);
                    Assert.Equal((int)HttpStatusCode.BadRequest, statusCodeResult.StatusCode);

                    _userServiceMock.VerifyAll();
                    packageOwnershipManagementServiceMock.Verify(m => m.AddPackageOwnerAsync(It.IsAny<PackageRegistration>(), _requiredCoOwner, false), Times.Never, "Required co-owner should not be added for non-compliant package.");
                    controller.MockPackageService.VerifyAll();
                    controller.MockPackageService.VerifyAll();
                }

                private static ISecurityPolicyService CreateSecurityPolicyService(
                    Lazy<IUserService> userServiceFactory,
                    Lazy<IPackageOwnershipManagementService> packageOwnershipManagementServiceFactory)
                {
                    var entitiesContext = new FakeEntitiesContext();
                    var auditing = new Mock<IAuditingService>().Object;
                    var diagnostics = new Mock<IDiagnosticsService>().Object;

                    var configurationMock = new Mock<IAppConfiguration>(MockBehavior.Strict);
                    configurationMock.SetupGet(m => m.EnforceDefaultSecurityPolicies).Returns(false);

                    var telemetryServiceMock = new Mock<ITelemetryService>();

                    return new SecurityPolicyService(
                        entitiesContext,
                        auditing,
                        diagnostics,
                        configurationMock.Object,
                        userServiceFactory,
                        packageOwnershipManagementServiceFactory,
                        telemetryServiceMock.Object);
                }

                private static Mock<TestPackageReader> CreatePackage(
                    string id,
                    string version,
                    bool isSigned,
                    string authors,
                    Uri licenseUrl,
                    Uri projectUrl)
                {
                    return PackageServiceUtility.CreateNuGetPackage(
                        id: id,
                        version: version,
                        isSigned: isSigned,
                        authors: authors,
                        copyright: "(c) Microsoft Corporation. All rights reserved.",
                        projectUrl: projectUrl,
                        licenseUrl: licenseUrl);
                }
            }
        }

        public class TheDeletePackageAction
            : TestContainer
        {
            [Fact]
            public async Task WillThrowIfAPackageWithTheIdAndNuGetVersionDoesNotExist()
            {
                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict("theId", "1.0.42")).Returns((Package)null);
                controller.SetCurrentUser(new User());

                var result = await controller.DeletePackage("theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(404, statusCodeResult.StatusCode);
                Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"), statusCodeResult.StatusDescription);
                controller.MockPackageUpdateService.Verify(x => x.MarkPackageUnlistedAsync(It.IsAny<Package>(), true, true), Times.Never());
            }

            public static IEnumerable<object[]> WillNotUnlistThePackageIfScopesInvalid_Data => MemberDataHelper.Combine(
                InvalidScopes_Data,
                MemberDataHelper.AsDataSet("1.0.42", "invalidPackageVersion"));

            [Theory]
            [MemberData(nameof(WillNotUnlistThePackageIfScopesInvalid_Data))]
            public async Task WillNotUnlistThePackageIfScopesInvalid(ApiScopeEvaluationResult evaluationResult, HttpStatusCode expectedStatusCode, string description, string version)
            {
                var fakes = Get<Fakes>();
                var currentUser = fakes.User;

                var id = "theId";
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = id },
                    Version = version
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package);

                controller.SetCurrentUser(currentUser);

                controller.MockApiScopeEvaluator
                    .Setup(x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.UnlistOrRelistPackage,
                        package.PackageRegistration,
                        NuGetScopes.PackageUnlist))
                    .Returns(evaluationResult);

                var result = await controller.DeletePackage(id, version);

                ResultAssert.IsStatusCode(
                    result,
                    expectedStatusCode,
                    description);

                controller.MockPackageUpdateService.Verify(x => x.MarkPackageUnlistedAsync(package, true, true), Times.Never());
            }

            [Fact]
            public async Task WillUnlistThePackage()
            {
                var fakes = Get<Fakes>();
                var currentUser = fakes.User;

                var id = "theId";
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = id }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package);

                controller.SetCurrentUser(currentUser);

                ResultAssert.IsEmpty(await controller.DeletePackage(id, "1.0.42"));

                controller.MockPackageUpdateService.Verify(x => x.MarkPackageUnlistedAsync(package, true, true));

                controller.MockApiScopeEvaluator
                    .Verify(x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.UnlistOrRelistPackage,
                        package.PackageRegistration,
                        NuGetScopes.PackageUnlist));
            }

            [Fact]
            public async Task WillNotUnlistThePackageIfItIsLocked()
            {
                // Arrange
                const string PackageId = "theId";
                var owner = new User { Key = 1, EmailAddress = "owner@confirmed.com" };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = PackageId,
                        Owners = new[] { new User(), owner },
                        IsLocked = true
                    }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(package)
                    .Verifiable();

                controller.SetCurrentUser(owner);

                // Act
                var result = await controller.DeletePackage(PackageId, "1.0.42");

                // Assert
                var statusCodeResult = result as HttpStatusCodeWithBodyResult;

                Assert.NotNull(statusCodeResult);
                Assert.Equal((int)HttpStatusCode.Forbidden, statusCodeResult.StatusCode);
                Assert.Contains(PackageId, statusCodeResult.StatusDescription);

                controller.MockPackageService.VerifyAll();
            }
        }

        public class TheGetPackageAction
            : TestContainer
        {
            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task GetPackageReturns400ForEvilPackageName(bool isSymbolPackage)
            {
                var controller = new TestableApiController(GetConfigurationService());
                var result = await controller.GetPackageInternal("../..", "1.0.0.0", isSymbolPackage);
                var badRequestResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(400, badRequestResult.StatusCode);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task GetPackageReturns400ForEvilPackageVersion(bool isSymbolPackage)
            {
                var controller = new TestableApiController(GetConfigurationService());
                var result2 = await controller.GetPackageInternal("Foo", "10../..1.0", isSymbolPackage);
                var badRequestResult2 = (HttpStatusCodeWithBodyResult)result2;
                Assert.Equal(400, badRequestResult2.StatusCode);
            }

            [Fact]
            public async Task GetPackageReturns404IfPackageIsNotFound()
            {
                // Arrange
                const string packageId = "Baz";
                const string packageVersion = "1.0.1";
                var actionResult = new RedirectResult("http://foo");

                var controller = new TestableApiController(GetConfigurationService(), MockBehavior.Strict);
                controller.MockPackageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(packageId, packageVersion))
                    .Returns((Package)null).Verifiable();
                controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(It.IsAny<Uri>(), packageId, packageVersion))
                    .Returns(Task.FromResult<ActionResult>(actionResult))
                    .Verifiable();

                // Act
                var result = await controller.GetPackageInternal(packageId, packageVersion);

                // Assert
                Assert.IsType<RedirectResult>(result);
            }

            [Fact]
            public async Task GetPackageReturns404ForSymbolPackageIfPackageIsNotFound()
            {
                // Arrange
                const string packageId = "Baz";
                const string packageVersion = "1.0.1";

                var controller = new TestableApiController(GetConfigurationService(), MockBehavior.Strict);
                controller.MockPackageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(packageId, packageVersion))
                    .Returns((Package)null).Verifiable();

                // Act
                var result = (HttpStatusCodeWithBodyResult)await controller.GetPackageInternal(packageId, packageVersion, isSymbolPackage: true);

                // Assert
                Assert.Equal((int)HttpStatusCode.NotFound, result.StatusCode);
            }

            [Theory]
            [InlineData(PackageStatus.Deleted)]
            [InlineData(PackageStatus.FailedValidation)]
            [InlineData(PackageStatus.Validating)]
            public async Task GetPackageReturnsLastAvailableSymbolPackage(PackageStatus status)
            {
                // Arrange
                const string packageId = "Baz";
                const string packageVersion = "1.0.1";
                var package = new Package() { PackageRegistration = new PackageRegistration() { Id = packageId }, Version = packageVersion };
                var latestSymbolPackage = new SymbolPackage()
                {
                    Key = 1,
                    Package = package,
                    StatusKey = status,
                    Created = DateTime.Today.AddDays(-1)
                };
                var oldAvailableSymbolPackage = new SymbolPackage()
                {
                    Key = 2,
                    Package = package,
                    StatusKey = PackageStatus.Available,
                    Created = DateTime.Today.AddDays(-2)
                };
                package.SymbolPackages.Add(oldAvailableSymbolPackage);
                package.SymbolPackages.Add(latestSymbolPackage);

                var controller = new TestableApiController(GetConfigurationService(), MockBehavior.Strict);
                controller.MockPackageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(packageId, packageVersion))
                    .Returns(package).Verifiable();
                controller.MockSymbolPackageFileService
                    .Setup(x => x.CreateDownloadSymbolPackageActionResultAsync(It.IsAny<Uri>(), packageId, packageVersion))
                    .Returns(Task.FromResult<ActionResult>(new HttpStatusCodeWithBodyResult(HttpStatusCode.OK, "Test package")))
                    .Verifiable();

                // Act
                var result = (HttpStatusCodeWithBodyResult)await controller.GetPackageInternal(packageId, packageVersion, isSymbolPackage: true);

                // Assert
                Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
                controller.MockSymbolPackageFileService.Verify();
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task GetPackageReturnsPackageIfItExists(bool isSymbolPackage)
            {
                // Arrange
                const string packageId = "Baz";
                var package = new Package() { Version = "1.0.01", NormalizedVersion = "1.0.1" };
                var actionResult = new EmptyResult();
                var availableSymbolPackage = new SymbolPackage()
                {
                    Key = 2,
                    Package = package,
                    StatusKey = PackageStatus.Available,
                    Created = DateTime.Today.AddDays(-1)
                };
                package.SymbolPackages.Add(availableSymbolPackage);

                var controller = new TestableApiController(GetConfigurationService(), MockBehavior.Strict);
                controller
                    .MockPackageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(packageId, "1.0.1"))
                    .Returns(package);
                controller
                    .MockPackageFileService
                    .Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, packageId, package.NormalizedVersion))
                    .Returns(Task.FromResult<ActionResult>(actionResult))
                    .Verifiable();
                controller
                    .MockSymbolPackageFileService
                    .Setup(s => s.CreateDownloadSymbolPackageActionResultAsync(HttpRequestUrl, packageId, package.NormalizedVersion))
                    .Returns(Task.FromResult<ActionResult>(actionResult))
                    .Verifiable();

                NameValueCollection headers = new NameValueCollection();
                headers.Add("NuGet-Operation", "Install");

                var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
                httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
                httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
                httpRequest.SetupGet(r => r.Headers).Returns(headers);
                httpRequest.SetupGet(r => r.Url).Returns(HttpRequestUrl);
                var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                // Act
                var result = await controller.GetPackageInternal(packageId, "1.0.01", isSymbolPackage);

                // Assert
                Assert.Same(actionResult, result);
                if (isSymbolPackage)
                {
                    controller.MockSymbolPackageFileService.Verify();
                }
                else
                {
                    controller.MockPackageFileService.Verify();
                }

                controller.MockPackageService.Verify();
                controller.MockUserService.Verify();
            }

            [Fact]
            public async Task GetPackageReturnsSpecificPackageEvenIfDatabaseIsOffline()
            {
                // Arrange
                var actionResult = new EmptyResult();

                var controller = new TestableApiController(GetConfigurationService(), MockBehavior.Strict);
                controller
                    .MockPackageFileService
                    .Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, "Baz", "1.0.0"))
                    .Returns(Task.FromResult<ActionResult>(actionResult))
                    .Verifiable();

                NameValueCollection headers = new NameValueCollection();
                headers.Add("NuGet-Operation", "Install");

                var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
                httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
                httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
                httpRequest.SetupGet(r => r.Headers).Returns(headers);
                httpRequest.SetupGet(r => r.Url).Returns(HttpRequestUrl);
                var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                // Act
                var result = await controller.GetPackageInternal("Baz", "1.0.0");

                // Assert
                Assert.Same(actionResult, result);
                controller.MockPackageFileService.Verify();
                controller.MockPackageService.Verify();
            }

            [Fact]
            public async Task GetPackageReturnsLatestPackageIfNoVersionIsProvided()
            {
                // Arrange
                const string packageId = "Baz";
                var package = new Package() { Version = "1.2.0408", NormalizedVersion = "1.2.408" };
                var actionResult = new EmptyResult();
                var controller = new TestableApiController(GetConfigurationService(), MockBehavior.Strict);
                controller.MockPackageService
                    .Setup(x => x.FindPackageByIdAndVersion(packageId, string.Empty, SemVerLevelKey.SemVer2, false))
                    .Returns(package);
                //controller.MockPackageService.Setup(x => x.AddDownloadStatistics(It.IsAny<PackageStatistics>())).Verifiable();

                controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, packageId, package.NormalizedVersion))
                              .Returns(Task.FromResult<ActionResult>(actionResult))
                              .Verifiable();

                NameValueCollection headers = new NameValueCollection();
                headers.Add("NuGet-Operation", "Install");

                var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
                httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
                httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
                httpRequest.SetupGet(r => r.Headers).Returns(headers);
                httpRequest.SetupGet(r => r.Url).Returns(HttpRequestUrl);
                var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                // Act
                var result = await controller.GetPackageInternal(packageId, "");

                // Assert
                Assert.Same(actionResult, result);
                controller.MockPackageFileService.Verify();
                controller.MockPackageService.Verify();
                controller.MockUserService.Verify();
            }

            [Fact]
            public async Task GetPackageReturns503IfNoVersionIsProvidedAndDatabaseUnavailable()
            {
                // Arrange
                const string packageId = "Baz";
                var package = new Package();
                var actionResult = new EmptyResult();
                var controller = new TestableApiController(GetConfigurationService(), MockBehavior.Strict);
                controller.MockPackageService
                    .Setup(x => x.FindPackageByIdAndVersion("Baz", string.Empty, SemVerLevelKey.SemVer2, false))
                    .Throws(new DataException("Oh noes, database broken!"));
                controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, packageId, package.NormalizedVersion))
                            .Returns(Task.FromResult<ActionResult>(actionResult))
                            .Verifiable();

                NameValueCollection headers = new NameValueCollection();
                headers.Add("NuGet-Operation", "Install");

                var httpRequest = new Mock<HttpRequestBase>(MockBehavior.Strict);
                httpRequest.SetupGet(r => r.UserHostAddress).Returns("Foo");
                httpRequest.SetupGet(r => r.UserAgent).Returns("Qux");
                httpRequest.SetupGet(r => r.Headers).Returns(headers);
                httpRequest.SetupGet(r => r.Url).Returns(HttpRequestUrl);
                var httpContext = new Mock<HttpContextBase>(MockBehavior.Strict);
                httpContext.SetupGet(c => c.Request).Returns(httpRequest.Object);

                var controllerContext = new ControllerContext(new RequestContext(httpContext.Object, new RouteData()), controller);
                controller.ControllerContext = controllerContext;

                // Act
                var result = await controller.GetPackageInternal("Baz", "");

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.ServiceUnavailable, Strings.DatabaseUnavailable_TrySpecificVersion);
                // controller.MockPackageFileService.Verify();
                controller.MockPackageService.Verify();
                // controller.MockUserService.Verify();
            }
        }

        public class ThePublishPackageAction
            : TestContainer
        {
            [Fact]
            public async Task WillThrowIfAPackageWithTheIdAndNuGetVersionDoesNotExist()
            {
                // Arrange
                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict("theId", "1.0.42")).Returns((Package)null);
                controller.SetCurrentUser(new User());

                // Act
                var result = await controller.PublishPackage("theId", "1.0.42");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.NotFound,
                    String.Format(Strings.PackageWithIdAndVersionNotFound, "theId", "1.0.42"));
                controller.MockPackageUpdateService.Verify(x => x.MarkPackageListedAsync(It.IsAny<Package>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
            }

            public static IEnumerable<object[]> WillNotListThePackageIfScopesInvalid_Data => MemberDataHelper.Combine(
                InvalidScopes_Data,
                MemberDataHelper.AsDataSet("1.0.42", "invalidPackageVersion"));

            [Theory]
            [MemberData(nameof(WillNotListThePackageIfScopesInvalid_Data))]
            public async Task WillNotListThePackageIfScopesInvalid(ApiScopeEvaluationResult evaluationResult, HttpStatusCode expectedStatusCode, string description, string version)
            {
                var fakes = Get<Fakes>();
                var currentUser = fakes.User;

                var id = "theId";
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = id },
                    Version = version
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package);

                controller.SetCurrentUser(currentUser);

                controller.MockApiScopeEvaluator
                    .Setup(x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.UnlistOrRelistPackage,
                        package.PackageRegistration,
                        NuGetScopes.PackageUnlist))
                    .Returns(evaluationResult);

                var result = await controller.PublishPackage(id, version);

                ResultAssert.IsStatusCode(
                    result,
                    expectedStatusCode,
                    description);

                controller.MockPackageUpdateService.Verify(x => x.MarkPackageListedAsync(package, true, It.IsAny<bool>()), Times.Never());
            }

            [Fact]
            public async Task WillListThePackage()
            {
                var fakes = Get<Fakes>();
                var currentUser = fakes.User;

                var id = "theId";
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = id }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package);

                controller.SetCurrentUser(currentUser);

                ResultAssert.IsEmpty(await controller.PublishPackage("theId", "1.0.42"));

                controller.MockPackageUpdateService.Verify(x => x.MarkPackageListedAsync(package, true, true));

                controller.MockApiScopeEvaluator
                    .Verify(x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.UnlistOrRelistPackage,
                        package.PackageRegistration,
                        NuGetScopes.PackageUnlist));
            }

            [Fact]
            public async Task WillNotListThePackageIfItIsLocked()
            {
                // Arrange
                const string PackageId = "theId";
                var owner = new User { Key = 1, EmailAddress = "owner@confirmed.com" };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = PackageId,
                        Owners = new[] { new User(), owner },
                        IsLocked = true
                    }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(package)
                    .Verifiable();

                controller.SetCurrentUser(owner);

                // Act
                var result = await controller.PublishPackage(PackageId, "1.0.42");

                // Assert
                var statusCodeResult = result as HttpStatusCodeWithBodyResult;

                Assert.NotNull(statusCodeResult);
                Assert.Equal((int)HttpStatusCode.Forbidden, statusCodeResult.StatusCode);
                Assert.Contains(PackageId, statusCodeResult.StatusDescription);

                controller.MockPackageService.VerifyAll();
            }

            public static IEnumerable<object[]> TrimsStatusDescriptionData()
            {
                yield return new object[] { "HelloWorld", 66 };
                yield return new object[] { new string('a', 1000), 512 };
            }

            [Theory]
            [MemberData(nameof(TrimsStatusDescriptionData))]
            public async Task TrimsStatusDescription(string packageId, int expectedLength)
            {
                // Arrange - Act like the package doesn't exist so that a status description is generated
                // detailing the package does not exist. If the package's name is too long, the status description
                // should be trimmed.
                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(packageId, "1.0.0")).Returns((Package)null);
                controller.SetCurrentUser(new User());

                // Act
                var result = await controller.PublishPackage(packageId, "1.0.0");

                // Assert
                var statusCodeResult = Assert.IsAssignableFrom<HttpStatusCodeResult>(result);

                Assert.Equal((int)HttpStatusCode.NotFound, statusCodeResult.StatusCode);
                Assert.Equal(expectedLength, statusCodeResult.StatusDescription.Length);
            }
        }

        public class TheDeprecatePackageAction : TestContainer
        {
            [Fact]
            public async Task WillThrowIfAPackageWithTheIdDoesNotExist()
            {
                // Arrange

                var id = "theId";

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService
                    .Setup(x => x.FindPackageRegistrationById(id))
                    .Returns((PackageRegistration)null);

                // Act
                var result = await controller.DeprecatePackage(id, versions: null);

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.NotFound,
                    string.Format(Strings.PackagesWithIdNotFound, id));

                controller.MockPackageDeprecationManagementService
                    .Verify(
                        x => x.UpdateDeprecation(
                            It.IsAny<User>(),
                            It.IsAny<string>(),
                            It.IsAny<IReadOnlyCollection<string>>(),
                            It.IsAny<bool>(),
                            It.IsAny<bool>(),
                            It.IsAny<bool>(),
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<string>()),
                        Times.Never());
            }

            public static IEnumerable<object[]> WillNotDeprecateThePackageIfScopesInvalid_Data = InvalidScopes_Data;

            [Theory]
            [MemberData(nameof(WillNotDeprecateThePackageIfScopesInvalid_Data))]
            public async Task WillNotDeprecateThePackageIfScopesInvalid(ApiScopeEvaluationResult evaluationResult, HttpStatusCode expectedStatusCode, string description)
            {
                var fakes = Get<Fakes>();
                var currentUser = fakes.User;

                var id = "theId";
                var registration = new PackageRegistration { Id = id };

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService
                    .Setup(x => x.FindPackageRegistrationById(id))
                    .Returns(registration);

                controller.SetCurrentUser(currentUser);

                controller.MockApiScopeEvaluator
                    .Setup(x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.DeprecatePackage,
                        registration,
                        NuGetScopes.PackageUnlist))
                    .Returns(evaluationResult);

                var result = await controller.DeprecatePackage(id, versions: null);

                ResultAssert.IsStatusCode(
                    result,
                    expectedStatusCode,
                    description);

                controller.MockPackageDeprecationManagementService
                    .Verify(
                        x => x.UpdateDeprecation(
                            It.IsAny<User>(), 
                            It.IsAny<string>(), 
                            It.IsAny<IReadOnlyCollection<string>>(),
                            It.IsAny<bool>(),
                            It.IsAny<bool>(),
                            It.IsAny<bool>(),
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<string>()),
                        Times.Never());
            }

            [Fact]
            public async Task ReturnsForbiddenIfFeatureFlagDisabled()
            {
                // Arrange
                var id = "Crested.Gecko";
                var versions = new[] { "1.0.0", "2.0.0" };

                var fakes = Get<Fakes>();
                var currentUser = fakes.User;
                var owner = fakes.Owner;

                var deprecationService = GetMock<IPackageDeprecationManagementService>();
                deprecationService
                    .Verify(
                        x => x.UpdateDeprecation(
                            It.IsAny<User>(),
                            It.IsAny<string>(),
                            It.IsAny<IReadOnlyCollection<string>>(),
                            It.IsAny<bool>(),
                            It.IsAny<bool>(),
                            It.IsAny<bool>(),
                            It.IsAny<string>(),
                            It.IsAny<string>(),
                            It.IsAny<string>()),
                        Times.Never());

                var registration = new PackageRegistration { Id = id };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageRegistrationById(id))
                    .Returns(registration);

                var scopeEvaluator = GetMock<IApiScopeEvaluator>();
                scopeEvaluator
                    .Setup(x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.DeprecatePackage,
                        registration,
                        NuGetScopes.PackageUnlist))
                    .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, true))
                    .Verifiable();

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationApiEnabled(owner))
                    .Returns(false)
                    .Verifiable();

                var controller = GetController<ApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.DeprecatePackage(
                    id,
                    versions);

                // Assert
                var statusCodeResult = result as HttpStatusCodeWithBodyResult;
                Assert.Equal((int)HttpStatusCode.Forbidden, statusCodeResult.StatusCode);
                Assert.Equal(Strings.ApiKeyNotAuthorized, statusCodeResult.Body);

                packageService.Verify();
                scopeEvaluator.Verify();
                featureFlagService.Verify();
                deprecationService.Verify();
            }

            public static IEnumerable<object[]> ReturnsProperResult_Data =
                MemberDataHelper.Combine(
                    Enumerable
                        .Repeat(
                            MemberDataHelper.BooleanDataSet(), 4)
                        .ToArray());

            [Theory]
            [MemberData(nameof(ReturnsProperResult_Data))]
            public async Task ReturnsProperResult(
                bool isLegacy,
                bool hasCriticalBugs,
                bool isOther,
                bool success)
            {
                // Arrange
                var id = "Crested.Gecko";
                var versions = new[] { "1.0.0", "2.0.0" };
                var alternateId = "alt.Id";
                var alternateVersion = "3.0.0";
                var customMessage = "custom";

                var fakes = Get<Fakes>();
                var currentUser = fakes.User;
                var owner = fakes.Owner;

                var errorStatus = HttpStatusCode.InternalServerError;
                var errorMessage = "woops";
                var deprecationService = GetMock<IPackageDeprecationManagementService>();
                deprecationService
                    .Setup(x => x.UpdateDeprecation(
                        owner,
                        id,
                        versions,
                        isLegacy,
                        hasCriticalBugs,
                        isOther,
                        alternateId,
                        alternateVersion,
                        customMessage))
                    .ReturnsAsync(success ? null : new UpdateDeprecationError(errorStatus, errorMessage))
                    .Verifiable();

                var registration = new PackageRegistration { Id = id };

                var packageService = GetMock<IPackageService>();
                packageService
                    .Setup(x => x.FindPackageRegistrationById(id))
                    .Returns(registration);

                var scopeEvaluator = GetMock<IApiScopeEvaluator>();
                scopeEvaluator
                    .Setup(x => x.Evaluate(
                        currentUser,
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.DeprecatePackage,
                        registration,
                        NuGetScopes.PackageUnlist))
                    .Returns(new ApiScopeEvaluationResult(owner, PermissionsCheckResult.Allowed, true))
                    .Verifiable();

                var featureFlagService = GetMock<IFeatureFlagService>();
                featureFlagService
                    .Setup(x => x.IsManageDeprecationApiEnabled(owner))
                    .Returns(true)
                    .Verifiable();

                var controller = GetController<ApiController>();
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await controller.DeprecatePackage(
                    id,
                    versions,
                    isLegacy,
                    hasCriticalBugs,
                    isOther,
                    alternateId,
                    alternateVersion,
                    customMessage);

                // Assert
                if (success)
                {
                    ResultAssert.IsEmpty(
                        result);
                }
                else
                {
                    var statusCodeResult = result as HttpStatusCodeWithBodyResult;
                    Assert.Equal((int)errorStatus, statusCodeResult.StatusCode);
                    Assert.Equal(errorMessage, statusCodeResult.Body);
                }

                packageService.Verify();
                scopeEvaluator.Verify();
                featureFlagService.Verify();
                deprecationService.Verify();
            }
        }

        public class PackageVerificationKeyContainer : TestContainer
        {
            public static int UserKey = 1234;
            public static string Username = "testuser";
            public static string PackageId = "foo";
            public static string PackageVersion = "1.0.0";

            internal TestableApiController SetupController(string keyType, Scope scope, Package package, bool isOwner = true)
            {
                var credential = new Credential(keyType, string.Empty, TimeSpan.FromDays(1));
                if (scope != null)
                {
                    credential.Scopes.Add(scope);
                }

                var user = Get<Fakes>().CreateUser(Username);
                user.EmailAddress = "confirmed@email.com";
                user.Key = UserKey;
                user.Credentials.Add(credential);

                if (package != null && isOwner)
                {
                    package.PackageRegistration.Owners.Add(user);
                }

                var controller = new TestableApiController(GetConfigurationService());

                controller.MockAuthenticationService
                    .Setup(s => s.AddCredential(It.IsAny<User>(), It.IsAny<Credential>()))
                    .Callback<User, Credential>((u, c) => u.Credentials.Add(c))
                    .Returns(Task.CompletedTask);

                controller.MockAuthenticationService
                    .Setup(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>(), true))
                    .Callback<User, Credential, bool>((u, c, cc) => u.Credentials.Remove(c))
                    .Returns(Task.CompletedTask);

                var id = package?.PackageRegistration?.Id ?? PackageId;
                var version = package?.Version ?? PackageVersion;
                controller.MockPackageService
                    .Setup(s => s.FindPackageByIdAndVersion(id, version, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                controller.MockUserService
                    .Setup(x => x.FindByKey(user.Key, false))
                    .Returns(user);

                controller.SetCurrentUser(user, credential);

                return controller;
            }
        }

        public class TheCreatePackageVerificationKeyAsyncAction
            : PackageVerificationKeyContainer
        {
            [Fact]
            public async Task WhenApiKeyHasNoScope_TempKeyHasScopeWithNoOwner()
            {
                var tempScope = await InvokeAsync(null);

                Assert.Null(tempScope.OwnerKey);
                Assert.Equal(PackageId, tempScope.Subject);
                Assert.Equal(NuGetScopes.PackageVerify, tempScope.AllowedAction);
            }

            public static IEnumerable<object[]> TempKeyHasScopeWithNoOwner_Data
            {
                get
                {
                    foreach (var allowedAction in new[] { NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion })
                    {
                        yield return new object[]
                        {
                            allowedAction
                        };
                    }
                }
            }

            [Theory]
            [MemberData(nameof(TempKeyHasScopeWithNoOwner_Data))]
            public async Task WhenApiKeyHasNoOwnerScope_TempKeyHasScopeWithNoOwner(string allowedAction)
            {
                var tempScope = await InvokeAsync(new Scope() { OwnerKey = null, Subject = PackageId, AllowedAction = allowedAction });

                Assert.Null(tempScope.OwnerKey);
                Assert.Equal(PackageId, tempScope.Subject);
                Assert.Equal(NuGetScopes.PackageVerify, tempScope.AllowedAction);
            }

            [Theory]
            [MemberData(nameof(TempKeyHasScopeWithNoOwner_Data))]
            public async Task WhenApiKeyHasOwnerScope_TempKeyHasSameOwner(string allowedAction)
            {
                var tempScope = await InvokeAsync(new Scope() { OwnerKey = 1234, Subject = PackageId, AllowedAction = allowedAction });

                Assert.Equal(1234, tempScope.OwnerKey);
                Assert.Equal(PackageId, tempScope.Subject);
                Assert.Equal(NuGetScopes.PackageVerify, tempScope.AllowedAction);
            }

            private async Task<Scope> InvokeAsync(Scope scope)
            {
                // Arrange
                var controller = SetupController(CredentialTypes.ApiKey.V4, scope, package: null);

                // Act
                var jsonResult = await controller.CreatePackageVerificationKeyAsync(PackageId, PackageVersion) as JsonResult;

                // Assert - the response
                dynamic json = jsonResult?.Data;
                Assert.NotNull(json);

                Guid key;
                Assert.True(Guid.TryParse(json.Key, out key));

                DateTime expires;
                Assert.True(DateTime.TryParse(json.Expires, out expires));

                // Assert - the invocations
                controller.MockAuthenticationService.Verify(s => s.AddCredential(It.IsAny<User>(), It.IsAny<Credential>()), Times.Once);

                controller.MockTelemetryService.Verify(x => x.TrackCreatePackageVerificationKeyEvent(PackageId, PackageVersion,
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity), Times.Once);

                // Assert - the temp key
                var user = controller.GetCurrentUser();
                var tempKey = user.Credentials.Last();

                Assert.Equal(1, tempKey.Scopes.Count);
                return tempKey.Scopes.First();
            }
        }

        public class TheVerifyPackageKeyAsyncAction
            : PackageVerificationKeyContainer
        {
            private static IEnumerable<string> AllCredentialTypes = new[]
            {
                CredentialTypes.ApiKey.V1,
                CredentialTypes.ApiKey.V2,
                CredentialTypes.ApiKey.V4,
                CredentialTypes.ApiKey.VerifyV1
            };

            private static IEnumerable<string> CredentialTypesExceptVerifyV1 =
                AllCredentialTypes
                    .Except(new[] { CredentialTypes.ApiKey.VerifyV1 });

            public static IEnumerable<object[]> AllCredentialTypes_Data =>
                AllCredentialTypes
                    .Select(t => MemberDataHelper.AsData(t));

            public static IEnumerable<object[]> CredentialTypesExceptVerifyV1_Data =>
                CredentialTypesExceptVerifyV1
                    .Select(t => MemberDataHelper.AsData(t));

            [Theory]
            [MemberData(nameof(AllCredentialTypes_Data))]
            public async Task Returns400IfSecurityPolicyFails(string credentialType)
            {
                // Arrange
                var errorResult = "A";
                var controller = SetupController(credentialType, null, package: null);
                controller.MockSecurityPolicyService.Setup(s => s.EvaluateUserPoliciesAsync(It.IsAny<SecurityPolicyAction>(), It.IsAny<User>(), It.IsAny<HttpContextBase>()))
                    .Returns(Task.FromResult(SecurityPolicyResult.CreateErrorResult(errorResult)));

                // Act
                var result = await controller.VerifyPackageKeyAsync(PackageId, PackageVersion);

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest, errorResult);
            }

            [Theory]
            [MemberData(nameof(AllCredentialTypes_Data))]
            public async Task Returns404IfPackageDoesNotExist(string credentialType)
            {
                // Arrange
                var controller = SetupController(credentialType, null, package: null);

                // Act
                var result = await controller.VerifyPackageKeyAsync(PackageId, PackageVersion);

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.NotFound,
                    String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, PackageId, PackageVersion));

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>(), true),
                    CredentialTypes.IsPackageVerificationApiKey(credentialType) ? Times.Once() : Times.Never());

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent(PackageId, PackageVersion,
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 404), Times.Once);
            }

            public static IEnumerable<object[]> Returns403IfScopeDoesNotMatch_PackageVersion_Data =>
                MemberDataHelper.AsDataSet("1.0.42", "invalidVersionString");

            public static IEnumerable<object[]> Returns403IfScopeDoesNotMatch_Data => InvalidScopes_Data;

            public static IEnumerable<object[]> Returns403IfScopeDoesNotMatch_NotVerify_Data
            {
                get
                {
                    var notVerifyData = CredentialTypesExceptVerifyV1.Select(
                        t => MemberDataHelper.AsData(t, new[] { NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion }));
                    return MemberDataHelper.Combine(
                        notVerifyData,
                        Returns403IfScopeDoesNotMatch_Data,
                        Returns403IfScopeDoesNotMatch_PackageVersion_Data);
                }
            }

            public static IEnumerable<object[]> Returns403IfScopeDoesNotMatch_Verify_Data
            {
                get
                {
                    return MemberDataHelper.Combine(
                        new[] { new object[] { CredentialTypes.ApiKey.VerifyV1, new[] { NuGetScopes.PackageVerify } } },
                        Returns403IfScopeDoesNotMatch_Data,
                        Returns403IfScopeDoesNotMatch_PackageVersion_Data);
                }
            }

            [Theory]
            [MemberData(nameof(Returns403IfScopeDoesNotMatch_NotVerify_Data))]
            [MemberData(nameof(Returns403IfScopeDoesNotMatch_Verify_Data))]
            public async Task Returns403IfScopeDoesNotMatch(string credentialType, string[] expectedRequestedActions, ApiScopeEvaluationResult apiScopeEvaluationResult, HttpStatusCode expectedStatusCode, string description, string packageVersion)
            {
                // Arrange
                PackageVersion = packageVersion;
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration() { Id = PackageId },
                    Version = PackageVersion
                };
                var controller = SetupController(credentialType, null, package);

                controller.MockApiScopeEvaluator
                    .Setup(x => x.Evaluate(
                        It.Is<User>(u => u.Key == UserKey),
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.VerifyPackage,
                        package.PackageRegistration,
                        expectedRequestedActions))
                    .Returns(apiScopeEvaluationResult);

                // Act
                var result = await controller.VerifyPackageKeyAsync(PackageId, PackageVersion);

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    expectedStatusCode,
                    description);

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>(), true),
                    CredentialTypes.IsPackageVerificationApiKey(credentialType) ? Times.Once() : Times.Never());

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent(PackageId, PackageVersion,
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, (int)expectedStatusCode), Times.Once);
            }

            [Fact]
            public Task Returns200_VerifyV1()
            {
                return Returns200(CredentialTypes.ApiKey.VerifyV1, true, NuGetScopes.PackageVerify);
            }

            [Theory]
            [MemberData(nameof(CredentialTypesExceptVerifyV1_Data))]
            public Task Returns200_NotVerify(string credentialType)
            {
                return Returns200(credentialType, false, NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion);
            }

            private async Task Returns200(string credentialType, bool isRemoved, params string[] expectedRequestedActions)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration() { Id = PackageId },
                    Version = PackageVersion
                };
                var controller = SetupController(credentialType, null, package);

                // Act
                var result = await controller.VerifyPackageKeyAsync(PackageId, PackageVersion);

                // Assert
                ResultAssert.IsEmpty(result);

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>(), true), isRemoved ? Times.Once() : Times.Never());

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent(PackageId, PackageVersion,
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 200), Times.Once);

                controller.MockApiScopeEvaluator
                    .Verify(x => x.Evaluate(
                        It.Is<User>(u => u.Key == UserKey),
                        It.IsAny<IEnumerable<Scope>>(),
                        ActionsRequiringPermissions.VerifyPackage,
                        package.PackageRegistration,
                        expectedRequestedActions));
            }

            [Fact]
            public async Task WritesAuditRecord()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration() { Id = PackageId },
                    Version = PackageVersion
                };
                var controller = SetupController(CredentialTypes.ApiKey.V4, null, package);

                // Act
                var result = await controller.VerifyPackageKeyAsync(PackageId, PackageVersion);

                // Assert
                Assert.True(controller.AuditingService.WroteRecord<PackageAuditRecord>(ar =>
                    ar.Action == AuditedPackageAction.Verify
                    && ar.Id == package.PackageRegistration.Id
                    && ar.Version == package.Version));
            }
        }

        public class TheGetStatsDownloadsAction
            : TestContainer
        {
            [Fact]
            public async Task VerifyRecentPopularityStatsDownloads()
            {
                JArray report = new JArray
                {
                    new JObject
                    {
                        { "PackageId", "A" },
                        { "PackageVersion", "1.0" },
                        { "Downloads", 3 }
                    },
                    new JObject
                    {
                        { "PackageId", "A" },
                        { "PackageVersion", "1.1" },
                        { "Downloads", 4 }
                    },
                    new JObject
                    {
                        { "PackageId", "B" },
                        { "PackageVersion", "1.0" },
                        { "Downloads", 5 }
                    },
                    new JObject
                    {
                        { "PackageId", "B" },
                        { "PackageVersion", "1.1" },
                        { "Downloads", 6 }
                    },
                };

                var fakePackageVersionReport = report.ToString();

                var fakeReportService = new Mock<IReportService>();

                fakeReportService
                    .Setup(x => x.Load("recentpopularitydetail.json"))
                    .Returns(Task.FromResult(new StatisticsReport(fakePackageVersionReport, DateTime.UtcNow)));

                var controller = new TestableApiController(GetConfigurationService())
                {
                    StatisticsService = new JsonStatisticsService(fakeReportService.Object),
                };

                TestUtility.SetupUrlHelperForUrlGeneration(controller);

                ActionResult actionResult = await controller.GetStatsDownloads(null);

                ContentResult contentResult = (ContentResult)actionResult;

                JArray result = JArray.Parse(contentResult.Content);

                Assert.True((string)result[3]["Gallery"] == "/packages/B/1.1", "unexpected content result[3].Gallery");
                Assert.True((int)result[2]["Downloads"] == 5, "unexpected content result[2].Downloads");
            }

            [Fact]
            public async Task VerifyStatsDownloadsReturnsNotFoundWhenStatsNotAvailable()
            {
                var controller = new TestableApiController(GetConfigurationService());
                controller.MockStatisticsService.Setup(x => x.PackageVersionDownloadsResult).Returns(StatisticsReportResult.Failed);

                TestUtility.SetupUrlHelperForUrlGeneration(controller);

                ActionResult actionResult = await controller.GetStatsDownloads(null);

                ResultAssert.IsStatusCode(
                    actionResult,
                    HttpStatusCode.NotFound);
            }

            [Fact]
            public async Task VerifyRecentPopularityStatsDownloadsCount()
            {
                JArray report = new JArray
                {
                    new JObject { { "PackageId", "A" }, { "PackageVersion", "1.0" }, { "Downloads", 3 } },
                    new JObject { { "PackageId", "A" }, { "PackageVersion", "1.1" }, { "Downloads", 4 } },
                    new JObject { { "PackageId", "B" }, { "PackageVersion", "1.0" }, { "Downloads", 5 } },
                    new JObject { { "PackageId", "B" }, { "PackageVersion", "1.1" }, { "Downloads", 6 } },
                    new JObject { { "PackageId", "C" }, { "PackageVersion", "1.0" }, { "Downloads", 7 } },
                    new JObject { { "PackageId", "C" }, { "PackageVersion", "1.1" }, { "Downloads", 8 } },
                };

                var fakePackageVersionReport = report.ToString();

                var fakeReportService = new Mock<IReportService>();

                fakeReportService.Setup(x => x.Load("recentpopularitydetail.json")).Returns(Task.FromResult(new StatisticsReport(fakePackageVersionReport, DateTime.UtcNow)));

                var controller = new TestableApiController(GetConfigurationService())
                {
                    StatisticsService = new JsonStatisticsService(fakeReportService.Object),
                };

                TestUtility.SetupUrlHelperForUrlGeneration(controller);

                ActionResult actionResult = await controller.GetStatsDownloads(3);

                ContentResult contentResult = (ContentResult)actionResult;

                JArray result = JArray.Parse(contentResult.Content);

                Assert.True(result.Count == 3, "unexpected content");
            }
        }

        public class TheGetNuGetExeAction
            : TestContainer
        {
            [Fact]
            public void RedirectsToDist()
            {
                // Arrange
                var controller = new TestableApiController(GetConfigurationService());

                // Act
                var result = controller.GetNuGetExe();

                // Assert
                var redirect = Assert.IsType<RedirectResult>(result);
                Assert.False(redirect.Permanent, "The redirect should not be permanent");
                Assert.Equal("https://dist.nuget.org/win-x86-commandline/v2.8.6/nuget.exe", redirect.Url);
            }
        }

        public class SymbolsTestException : Exception
        {
            public SymbolsTestException(string message) : base(message)
            {

            }
        }

    }
}