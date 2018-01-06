// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using Xunit;

namespace NuGetGallery
{
    internal class TestableApiController
        : ApiController
    {
        public TestableApiScopeEvaluator TestableApiScopeEvaluator = new TestableApiScopeEvaluator();
        public Mock<IEntitiesContext> MockEntitiesContext { get; private set; }
        public Mock<IPackageService> MockPackageService { get; private set; }
        public Mock<IPackageFileService> MockPackageFileService { get; private set; }
        public Mock<IUserService> MockUserService { get; private set; }
        public Mock<INuGetExeDownloaderService> MockNuGetExeDownloaderService { get; private set; }
        public Mock<IContentService> MockContentService { get; private set; }
        public Mock<IStatisticsService> MockStatisticsService { get; private set; }
        public Mock<IIndexingService> MockIndexingService { get; private set; }
        public Mock<IAutomaticallyCuratePackageCommand> MockAutoCuratePackage { get; private set; }
        public Mock<IMessageService> MockMessageService { get; private set; }
        public Mock<ITelemetryService> MockTelemetryService { get; private set; }
        public Mock<AuthenticationService> MockAuthenticationService { get; private set; }
        public Mock<ISecurityPolicyService> MockSecurityPolicyService { get; private set; }
        public Mock<IReservedNamespaceService> MockReservedNamespaceService { get; private set; }
        public Mock<IPackageUploadService> MockPackageUploadService { get; private set; }

        private Stream PackageFromInputStream { get; set; }

        public TestableApiController(
            IGalleryConfigurationService configurationService,
            MockBehavior behavior = MockBehavior.Default)
        {
            SetOwinContextOverride(Fakes.CreateOwinContext());
            ApiScopeEvaluator = TestableApiScopeEvaluator;
            EntitiesContext = (MockEntitiesContext = new Mock<IEntitiesContext>()).Object;
            PackageService = (MockPackageService = new Mock<IPackageService>(behavior)).Object;
            UserService = (MockUserService = new Mock<IUserService>(behavior)).Object;
            NugetExeDownloaderService = (MockNuGetExeDownloaderService = new Mock<INuGetExeDownloaderService>(MockBehavior.Strict)).Object;
            ContentService = (MockContentService = new Mock<IContentService>()).Object;
            StatisticsService = (MockStatisticsService = new Mock<IStatisticsService>()).Object;
            IndexingService = (MockIndexingService = new Mock<IIndexingService>()).Object;
            AutoCuratePackage = (MockAutoCuratePackage = new Mock<IAutomaticallyCuratePackageCommand>()).Object;
            AuthenticationService = (MockAuthenticationService = new Mock<AuthenticationService>()).Object;
            SecurityPolicyService = (MockSecurityPolicyService = new Mock<ISecurityPolicyService>()).Object;
            ReservedNamespaceService = (MockReservedNamespaceService = new Mock<IReservedNamespaceService>()).Object;
            PackageUploadService = (MockPackageUploadService = new Mock<IPackageUploadService>()).Object;

            TestableApiScopeEvaluator.Result = ApiScopeEvaluationResult.Success;
            TestableApiScopeEvaluator.OwnerFactory = () => GetCurrentUser();

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

            MockSecurityPolicyService.Setup(s => s.EvaluateAsync(It.IsAny<SecurityPolicyAction>(), It.IsAny<HttpContextBase>()))
                .Returns(Task.FromResult(SecurityPolicyResult.SuccessResult));
            
            MockReservedNamespaceService
                .Setup(s => s.GetReservedNamespacesForId(It.IsAny<string>()))
                .Returns(new ReservedNamespace[0]);

            MockPackageUploadService.Setup(x => x.GeneratePackageAsync(It.IsAny<string>(), It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<User>()))
                .Returns((string id, PackageArchiveReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User owner, User currentUser) => {
                    var packageMetadata = PackageMetadata.FromNuspecReader(nugetPackage.GetNuspecReader());

                    var package = new Package();
                    package.PackageRegistration = new PackageRegistration { Id = packageMetadata.Id, IsVerified = false };
                    package.Version = packageMetadata.Version.ToString();
                    package.SemVerLevelKey = SemVerLevelKey.ForPackage(packageMetadata.Version, packageMetadata.GetDependencyGroups().AsPackageDependencyEnumerable());

                    return Task.FromResult(package);
                });

            var requestMock = new Mock<HttpRequestBase>();
            requestMock.Setup(m => m.IsSecureConnection).Returns(true);
            requestMock.Setup(m => m.Url).Returns(new Uri(TestUtility.GallerySiteRootHttps));

            var httpContextMock = new Mock<HttpContextBase>();
            httpContextMock.Setup(m => m.Request).Returns(requestMock.Object);

            TestUtility.SetupHttpContextMockForUrlGeneration(httpContextMock, this);
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

        public static IEnumerable<object[]> VerifyApiKeyScopes_Data(IEnumerable<string> correctActionScopes, IEnumerable<string> incorrectActionScopes)
        {
            foreach (var isOwnerScopeCorrect in new[] { false, true })
            {
                foreach (var isSubjectScopeCorrect in new[] { false, true })
                {
                    foreach (var isActionScopeCorrect in new[] { false, true })
                    {
                        foreach (var allowedAction in isActionScopeCorrect ? correctActionScopes : incorrectActionScopes)
                        {
                            yield return new object[]
                            {
                                    isOwnerScopeCorrect,
                                    isSubjectScopeCorrect,
                                    allowedAction
                            };
                        }
                    }
                }
            }
        }

        public class TheCreatePackageAction
            : TestContainer
        {
            [Fact]
            public async Task CreatePackage_Returns400IfSecurityPolicyFails()
            {
                // Arrange
                var controller = new TestableApiController(GetConfigurationService());
                controller.MockSecurityPolicyService.Setup(s => s.EvaluateAsync(It.IsAny<SecurityPolicyAction>(), It.IsAny<HttpContextBase>()))
                    .Returns(Task.FromResult(SecurityPolicyResult.CreateErrorResult("A")));

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest, "A");
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
            [InlineData(false, false,  true)]
            [InlineData( true, false,  true)]
            [InlineData(false,  true,  true)]
            [InlineData( true,  true, false)]
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
                    .Verify(ms => ms.SendPackageAddedNotice(package, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                    Times.Exactly(callExpected ? 1 : 0));
            }

            [Fact]
            public async Task CreatePackageWillReturn400IfFileIsNotANuGetPackage()
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

            [Fact]
            public async Task WillReturnConflictIfAPackageWithTheIdAndSameNormalizedVersionAlreadyExists()
            {
                var id = "theId";
                var version = "1.0.42";
                var nuGetPackage = TestPackage.CreateTestPackageStream(id, version);

                var user = new User() { EmailAddress = "confirmed@email.com" };

                var packageRegistration = new PackageRegistration
                {
                    Id = id,
                    Packages = new List<Package> { new Package { Version = version, NormalizedVersion = version } },
                    Owners = new List<User> { user }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(new User());
                controller.MockPackageService.Setup(x => x.FindPackageRegistrationById(id)).Returns(packageRegistration);
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Conflict,
                    String.Format(Strings.PackageExistsAndCannotBeModified, id, version));
            }

            [Fact]
            public async Task WillReturnConflictIfCommittingPackageReturnsConflict()
            {
                // Arrange
                var user = new User { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller
                    .MockPackageUploadService
                    .Setup(x => x.CommitPackageAsync(It.IsAny<Package>(), It.IsAny<Stream>()))
                    .ReturnsAsync(PackageCommitResult.Conflict);

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                controller.SetCurrentUser(new User());
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
                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, "1.0.42");

                var currentUser = new User("currentUser") { Key = 1, EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(currentUser);
                controller.SetupPackageFromInputStream(nuGetPackage);

                var owner = new User("owner") { Key = 2 };

                controller.TestableApiScopeEvaluator.OwnerFactory = () => owner;
                controller.TestableApiScopeEvaluator.Setup<ActionOnNewPackageContext>(
                    currentUser, 
                    ActionsRequiringPermissions.UploadNewPackageId, 
                    (context) => context.PackageId == packageId, 
                    NuGetScopes.PackagePush);

                await controller.CreatePackagePut();

                controller.MockPackageUploadService.Verify(
                    x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        owner,
                        currentUser),
                    Times.Once);
            }

            public static IEnumerable<object[]> WillNotCreateAPackageIfScopesInvalid_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(ApiScopeEvaluationResult.Forbidden, HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
                    yield return MemberDataHelper.AsData(ApiScopeEvaluationResult.ConflictReservedNamespace, HttpStatusCode.Conflict, Strings.UploadPackage_IdNamespaceConflict);
                }
            }

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

                controller.TestableApiScopeEvaluator.Result = scopeEvaluationResult;
                controller.TestableApiScopeEvaluator.Setup<ActionOnNewPackageContext>(
                    currentUser,
                    ActionsRequiringPermissions.UploadNewPackageId,
                    (context) => context.PackageId == packageId,
                    NuGetScopes.PackagePush);

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

                var owner = new User("owner") { Key = 2 };

                controller.TestableApiScopeEvaluator.OwnerFactory = () => owner;
                controller.TestableApiScopeEvaluator.Setup(
                    currentUser, 
                    ActionsRequiringPermissions.UploadNewPackageVersion, 
                    packageRegistration,
                    NuGetScopes.PackagePushVersion,
                    NuGetScopes.PackagePush);

                await controller.CreatePackagePut();

                controller.MockPackageUploadService.Verify(
                    x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        owner,
                        currentUser),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(WillNotCreateAPackageIfScopesInvalid_Data))]
            public async Task WillNotCreateAPackageIfScopesInvalidWithExistingRegistration(ApiScopeEvaluationResult scopeEvaluationResult, HttpStatusCode expectedStatusCode, string description)
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

                controller.TestableApiScopeEvaluator.Result = scopeEvaluationResult;
                controller.TestableApiScopeEvaluator.Setup(
                    currentUser,
                    ActionsRequiringPermissions.UploadNewPackageVersion,
                    packageRegistration,
                    NuGetScopes.PackagePushVersion,
                    NuGetScopes.PackagePush);

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
            public async Task WillCurateThePackage()
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                await controller.CreatePackagePut();

                controller.MockAutoCuratePackage.Verify(x => x.ExecuteAsync(It.IsAny<Package>(), It.IsAny<PackageArchiveReader>(), false));
            }

            [Fact]
            public async Task WillCurateThePackageViaApi()
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.SetupPackageFromInputStream(nuGetPackage);

                await controller.CreatePackagePost();

                controller.MockAutoCuratePackage.Verify(x => x.ExecuteAsync(It.IsAny<Package>(), It.IsAny<PackageArchiveReader>(), false));
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

                var user = new User() { EmailAddress = "confirmed@email.com" };
                var packageRegistration = new PackageRegistration
                {
                    Id = PackageId,
                    IsLocked = true,
                    Owners = new List<User> { user }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(new User());
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
                controller.MockPackageService.Verify(x => x.MarkPackageUnlistedAsync(It.IsAny<Package>(), true), Times.Never());
            }

            public static IEnumerable<object[]> WillNotUnlistThePackageIfScopesInvalid_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(ApiScopeEvaluationResult.Forbidden, HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
                    yield return MemberDataHelper.AsData(ApiScopeEvaluationResult.ConflictReservedNamespace, HttpStatusCode.Conflict, Strings.UploadPackage_IdNamespaceConflict);
                }
            }

            [Theory]
            [MemberData(nameof(WillNotUnlistThePackageIfScopesInvalid_Data))]
            public async Task WillNotUnlistThePackageIfScopesInvalid(ApiScopeEvaluationResult evaluationResult, HttpStatusCode expectedStatusCode, string description)
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

                controller.TestableApiScopeEvaluator.Setup(currentUser, ActionsRequiringPermissions.UnlistOrRelistPackage, package, NuGetScopes.PackageUnlist);
                controller.TestableApiScopeEvaluator.Result = evaluationResult;

                var result = await controller.DeletePackage("theId", "1.0.42");

                ResultAssert.IsStatusCode(
                    result,
                    expectedStatusCode,
                    description);

                controller.MockPackageService.Verify(x => x.MarkPackageUnlistedAsync(package, true), Times.Never());
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

                controller.TestableApiScopeEvaluator.Setup(currentUser, ActionsRequiringPermissions.UnlistOrRelistPackage, package, NuGetScopes.PackageUnlist);

                ResultAssert.IsEmpty(await controller.DeletePackage(id, "1.0.42"));

                controller.MockPackageService.Verify(x => x.MarkPackageUnlistedAsync(package, true));
                controller.MockIndexingService.Verify(i => i.UpdatePackage(package));
            }

            [Fact]
            public async Task WillNotUnlistThePackageIfItIsLocked()
            {
                // Arrange
                const string PackageId = "theId";
                var owner = new User { Key = 1 };
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
            [Fact]
            public async Task GetPackageReturns400ForEvilPackageName()
            {
                var controller = new TestableApiController(GetConfigurationService());
                var result = await controller.GetPackage("../..", "1.0.0.0");
                var badRequestResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(400, badRequestResult.StatusCode);
            }

            [Fact]
            public async Task GetPackageReturns400ForEvilPackageVersion()
            {
                var controller = new TestableApiController(GetConfigurationService());
                var result2 = await controller.GetPackage("Foo", "10../..1.0");
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
                    .Setup(x => x.FindPackageByIdAndVersion(packageId, packageVersion, SemVerLevelKey.SemVer2, false))
                    .Returns((Package)null).Verifiable();
                controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(It.IsAny<Uri>(), packageId, packageVersion))
                              .Returns(Task.FromResult<ActionResult>(actionResult))
                              .Verifiable();

                // Act
                var result = await controller.GetPackage(packageId, packageVersion);

                // Assert
                Assert.IsType<RedirectResult>(result); // all we want to check is that we're redirecting to storage
                //var httpNotFoundResult = (RedirectResult)result;
                //Assert.Equal(String.Format(Strings.PackageWithIdAndVersionNotFound, packageId, packageVersion), httpNotFoundResult.StatusDescription);
                //controller.MockPackageService.Verify();
            }

            [Fact]
            public async Task GetPackageReturnsPackageIfItExists()
            {
                // Arrange
                const string packageId = "Baz";
                var package = new Package() { Version = "1.0.01", NormalizedVersion = "1.0.1" };
                var actionResult = new EmptyResult();
                var controller = new TestableApiController(GetConfigurationService(), MockBehavior.Strict);
                // controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion(PackageId, "1.0.1", false)).Returns(package);
                // controller.MockPackageService.Setup(x => x.AddDownloadStatistics(It.IsAny<PackageStatistics>())).Verifiable();
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
                var result = await controller.GetPackage(packageId, "1.0.01");

                // Assert
                Assert.Same(actionResult, result);
                controller.MockPackageFileService.Verify();
                controller.MockPackageService.Verify();
                controller.MockUserService.Verify();
            }

            [Fact]
            public async Task GetPackageReturnsSpecificPackageEvenIfDatabaseIsOffline()
            {
                // Arrange
                var actionResult = new EmptyResult();

                var controller = new TestableApiController(GetConfigurationService(), MockBehavior.Strict);
                //controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersion("Baz", "1.0.0", false)).Throws(new DataException("Can't find the database")).Verifiable();
                controller.MockPackageFileService.Setup(s => s.CreateDownloadPackageActionResultAsync(HttpRequestUrl, "Baz", "1.0.0"))
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
                var result = await controller.GetPackage("Baz", "1.0.0");

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
                var result = await controller.GetPackage(packageId, "");

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
                var result = await controller.GetPackage("Baz", "");

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
                controller.MockPackageService.Verify(x => x.MarkPackageListedAsync(It.IsAny<Package>(), It.IsAny<bool>()), Times.Never());
            }

            public static IEnumerable<object[]> WillListThePackageIfScopesInvalid_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(ApiScopeEvaluationResult.Forbidden, HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
                    yield return MemberDataHelper.AsData(ApiScopeEvaluationResult.ConflictReservedNamespace, HttpStatusCode.Conflict, Strings.UploadPackage_IdNamespaceConflict);
                }
            }

            [Theory]
            [MemberData(nameof(WillListThePackageIfScopesInvalid_Data))]
            public async Task WillListThePackageIfScopesInvalid(ApiScopeEvaluationResult evaluationResult, HttpStatusCode expectedStatusCode, string description)
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

                controller.TestableApiScopeEvaluator.Setup(currentUser, ActionsRequiringPermissions.UnlistOrRelistPackage, package, NuGetScopes.PackageUnlist);
                controller.TestableApiScopeEvaluator.Result = evaluationResult;

                var result = await controller.PublishPackage("theId", "1.0.42");

                ResultAssert.IsStatusCode(
                    result,
                    expectedStatusCode,
                    description);

                controller.MockPackageService.Verify(x => x.MarkPackageListedAsync(package, true), Times.Never());
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

                controller.TestableApiScopeEvaluator.Setup(currentUser, ActionsRequiringPermissions.UnlistOrRelistPackage, package, NuGetScopes.PackageUnlist);
                
                ResultAssert.IsEmpty(await controller.PublishPackage("theId", "1.0.42"));

                controller.MockPackageService.Verify(x => x.MarkPackageListedAsync(package, true));
                controller.MockIndexingService.Verify(i => i.UpdatePackage(package));
            }

            [Fact]
            public async Task WillNotListThePackageIfItIsLocked()
            {
                // Arrange
                const string PackageId = "theId";
                var owner = new User { Key = 1 };
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
                    .Setup(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()))
                    .Callback<User, Credential>((u, c) => u.Credentials.Remove(c))
                    .Returns(Task.CompletedTask);

                var id = package?.PackageRegistration?.Id ?? PackageId;
                var version = package?.Version ?? PackageVersion;
                controller.MockPackageService
                    .Setup(s => s.FindPackageByIdAndVersion(id, version, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                controller.MockUserService
                    .Setup(x => x.FindByKey(user.Key))
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
                controller.MockSecurityPolicyService.Setup(s => s.EvaluateAsync(It.IsAny<SecurityPolicyAction>(), It.IsAny<HttpContextBase>()))
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

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()),
                    CredentialTypes.IsPackageVerificationApiKey(credentialType) ? Times.Once() : Times.Never());

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent(PackageId, PackageVersion,
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 404), Times.Once);
            }

            public static IEnumerable<object[]> Returns403IfScopeDoesNotMatch_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(ApiScopeEvaluationResult.Forbidden, HttpStatusCode.Forbidden, Strings.ApiKeyNotAuthorized);
                    yield return MemberDataHelper.AsData(ApiScopeEvaluationResult.ConflictReservedNamespace, HttpStatusCode.Conflict, Strings.UploadPackage_IdNamespaceConflict);
                }
            }

            public static IEnumerable<object[]> Returns403IfScopeDoesNotMatch_NotVerify_Data
            {
                get
                {
                    var notVerifyData = CredentialTypesExceptVerifyV1.Select(t => new object[] { t, new[] { NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion } });
                    return MemberDataHelper.Combine(notVerifyData, Returns403IfScopeDoesNotMatch_Data);
                }
            }

            public static IEnumerable<object[]> Returns403IfScopeDoesNotMatch_Verify_Data
            {
                get
                {
                    return MemberDataHelper.Combine(new[] { new object[] { CredentialTypes.ApiKey.VerifyV1, new[] { NuGetScopes.PackageVerify } } }, Returns403IfScopeDoesNotMatch_Data);
                }
            }

            [Theory]
            [MemberData(nameof(Returns403IfScopeDoesNotMatch_NotVerify_Data))]
            [MemberData(nameof(Returns403IfScopeDoesNotMatch_Verify_Data))]
            public async Task Returns403IfScopeDoesNotMatch(string credentialType, string[] expectedRequestedActions, ApiScopeEvaluationResult apiScopeEvaluationResult, HttpStatusCode expectedStatusCode, string description)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration() { Id = PackageId },
                    Version = PackageVersion
                };
                var controller = SetupController(credentialType, null, package);

                controller.TestableApiScopeEvaluator.Result = apiScopeEvaluationResult;
                controller.TestableApiScopeEvaluator.Setup(
                    expectedAction: ActionsRequiringPermissions.VerifyPackage, 
                    expectedEntity: package, 
                    expectedRequestedActions: expectedRequestedActions);

                // Act
                var result = await controller.VerifyPackageKeyAsync(PackageId, PackageVersion);

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    expectedStatusCode,
                    description);

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()),
                    CredentialTypes.IsPackageVerificationApiKey(credentialType) ? Times.Once() : Times.Never());

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent(PackageId, PackageVersion,
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, (int)expectedStatusCode), Times.Once);
            }
            
            [Fact]
            public Task Returns200_VerifyV1()
            {
                return Returns200(CredentialTypes.ApiKey.VerifyV1, true);
            }

            [Theory]
            [MemberData(nameof(CredentialTypesExceptVerifyV1_Data))]
            public Task Returns200_NotVerify(string credentialType)
            {
                return Returns200(credentialType, false);
            }

            private async Task Returns200(string credentialType, bool isRemoved)
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

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()), isRemoved ? Times.Once() : Times.Never());

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent(PackageId, PackageVersion,
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 200), Times.Once);
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
    }
}