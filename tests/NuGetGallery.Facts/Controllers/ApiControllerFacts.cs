﻿// Copyright (c) .NET Foundation. All rights reserved.
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGetGallery.Auditing;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using NuGetGallery.Infrastructure.Authentication;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using Xunit;

namespace NuGetGallery
{
    internal class TestableApiController
        : ApiController
    {
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

            MockPackageUploadService.Setup(x => x.GeneratePackageAsync(It.IsAny<string>(), It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>()))
                .Returns((string id, PackageArchiveReader nugetPackage, PackageStreamMetadata packageStreamMetadata, User user) => {
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

            [Fact]
            public async Task CreatePackageWillSendPackageAddedNotice()
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

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.MockMessageService.Setup(p => p.SendPackageUploadedNotice(package, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Verifiable();
                controller.MockPackageUploadService
                    .Setup(p => p.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>()))
                    .Returns(Task.FromResult(package));

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                await controller.CreatePackagePut();

                // Assert
                controller.MockMessageService.Verify();
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
            public async Task WillWriteAnAuditRecordIfUserIsNotPackageOwner()
            {
                // Arrange
                var user = new User { EmailAddress = "confirmed@email.com" };
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = "theId";
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.42";
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(packageRegistration);

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                await controller.CreatePackagePut();

                // Assert
                Assert.True(controller.AuditingService.WroteRecord<FailedAuthenticatedOperationAuditRecord>(ar =>
                    ar.Action == AuditedAuthenticatedOperationAction.PackagePushAttemptByNonOwner
                    && ar.AttemptedPackage.Id == package.PackageRegistration.Id
                    && ar.AttemptedPackage.Version == package.Version));
            }

            [Fact]
            public async Task WillReturnConflictIfAPackageWithTheIdAndSameNormalizedVersionAlreadyExists()
            {
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.042");

                var user = new User() { EmailAddress = "confirmed@email.com" };

                var packageRegistration = new PackageRegistration
                {
                    Packages = new List<Package> { new Package { Version = "01.00.42", NormalizedVersion = "1.0.42" } },
                    Owners = new List<User> { user }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(new User());
                controller.MockPackageService.Setup(x => x.FindPackageRegistrationById("theId")).Returns(packageRegistration);
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Conflict,
                    String.Format(Strings.PackageExistsAndCannotBeModified, "theId", "1.0.42"));
            }

            [Fact]
            public async Task WillReturnUnauthorizedIfAPackageWithTheIdExistsBelongingToAnotherUser()
            {
                // Arrange
                var user = new User { EmailAddress = "confirmed@email.com" };
                var packageId = "theId";
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = packageId;
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.42";
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user);
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(packageRegistration);

                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, "1.0.42");
                controller.SetCurrentUser(new User());
                controller.SetupPackageFromInputStream(nuGetPackage);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Unauthorized,
                    String.Format(Strings.ApiKeyNotAuthorized, packageId));
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
            public async Task WillReturnConflictIfAPackageWithTheIdMatchesNonOwnedNamespace()
            {
                // Arrange
                var user1 = new User { Key = 1, Username = "random1" };
                var user2 = new User { Key = 2, Username = "random2" };
                var packageId = "Random.Extention.Package1";
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = packageId;
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.0";
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user1);
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(() => null);

                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, "1.0.0");
                controller.SetupPackageFromInputStream(nuGetPackage);
                var testNamespace = new ReservedNamespace("random.", isSharedNamespace: false, isPrefix: true);
                testNamespace.Owners.Add(user2);
                IReadOnlyCollection<ReservedNamespace> matchingNamespaces = new List<ReservedNamespace> { testNamespace };
                controller.MockReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(matchingNamespaces);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Conflict,
                    String.Format(Strings.UploadPackage_IdNamespaceConflict));

                controller.MockTelemetryService.Verify(x => x.TrackPackagePushNamespaceConflictEvent(packageRegistration.Id, package.Version, user1, controller.OwinContext.Request.User.Identity), Times.Once);
            }

            [Fact]
            public async Task WillCreateAPackageFromTheNuGetPackage()
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
                        It.IsAny<User>()),
                    Times.Once);
            }

            [Fact]
            public async Task WillCreatePackageIfIdMatchesSharedNamespace()
            {
                // Arrange
                var user1 = new User { Key = 1, Username = "random1" };
                var user2 = new User { Key = 2, Username = "random2" };
                var packageId = "Random.Extention.Package1";
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = packageId;
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.0";
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user1);
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(() => null);

                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, "1.0.0");
                controller.SetupPackageFromInputStream(nuGetPackage);
                var testNamespace = new ReservedNamespace("random.", isSharedNamespace: true, isPrefix: true);
                testNamespace.Owners.Add(user2);
                IReadOnlyCollection<ReservedNamespace> matchingNamespaces = new List<ReservedNamespace> { testNamespace };
                controller.MockReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(matchingNamespaces);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                controller.MockPackageUploadService.Verify(
                    x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>()));
            }

            [Fact]
            public async Task WillCreatePackageIfIdMatchesAnOwnedNamespace()
            {
                // Arrange
                var user1 = new User { Key = 1, Username = "random1" };
                var packageId = "Random.Extention.Package1";
                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = packageId;
                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.0";
                packageRegistration.Packages.Add(package);

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user1);
                controller.MockPackageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(() => null);

                var nuGetPackage = TestPackage.CreateTestPackageStream(packageId, "1.0.0");
                controller.SetupPackageFromInputStream(nuGetPackage);
                var testNamespace = new ReservedNamespace("random.", isSharedNamespace: false, isPrefix: true);
                testNamespace.Owners.Add(user1);
                IReadOnlyCollection<ReservedNamespace> matchingNamespaces = new List<ReservedNamespace> { testNamespace };
                controller.MockReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(matchingNamespaces);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                controller.MockPackageUploadService.Verify(
                    x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>()),
                    Times.Once);
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
                        user),
                    Times.Once);
            }

            [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]", true)]
            [InlineData("[{\"a\":\"package:push\", \"s\":\"*\"}]", true)]
            [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"theId\"}]", false)]
            [InlineData("[{\"a\":\"package:push\", \"s\":\"cbd\"}]", false)]
            [Theory]
            public async Task WillVerifyScopesForNewPackageId(string apiKeyScopes, bool isPushAllowed)
            {
                // Arrange
                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User { EmailAddress = "confirmed@email.com", Username = "username" };

                var package = new Package();
                package.PackageRegistration = new PackageRegistration();
                package.Version = "1.0.42";

                var credential = TestCredentialHelper.CreateV4ApiKey(expiration: null, plaintextApiKey: out string plaintextApiKey);
                credential.Scopes = JsonConvert.DeserializeObject<List<Scope>>(apiKeyScopes);

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user, credential);
                controller.SetupPackageFromInputStream(nuGetPackage);
                controller.MockPackageUploadService
                    .Setup(x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        user))
                    .ReturnsAsync(package);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                if (isPushAllowed)
                {
                    controller.MockPackageUploadService.Verify(
                        x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            user));
                }
                else
                {
                    controller.MockPackageUploadService.Verify(
                        x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>()),
                        Times.Never);

                    ResultAssert.IsStatusCode(
                        result,
                        HttpStatusCode.Unauthorized,
                        Strings.ApiKeyNotAuthorized);
                }
            }

            [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"differentid\"}]", false)]
            [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]", true)]
            [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"theId\"}]", true)]
            [Theory]
            public async Task WillVerifyScopesForExistingPackageId(string apiKeyScopes, bool isPushAllowed)
            {
                // Arrange
                const string packageId = "theId";

                var nuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.42");

                var user = new User { EmailAddress = "confirmed@email.com", Username = "username", Key = 1 };

                var credential = TestCredentialHelper.CreateV4ApiKey(expiration: null, plaintextApiKey: out string plaintextApiKey);
                credential.Scopes = JsonConvert.DeserializeObject<List<Scope>>(apiKeyScopes);

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(user, credential);
                controller.SetupPackageFromInputStream(nuGetPackage);

                var packageRegistration = new PackageRegistration();
                packageRegistration.Id = packageId;
                packageRegistration.Owners.Add(user);

                var package = new Package();
                package.PackageRegistration = packageRegistration;
                package.Version = "1.0.42";

                controller.MockPackageService.Setup(x => x.FindPackageRegistrationById(packageId))
                    .Returns(packageRegistration);
                controller.MockPackageUploadService
                    .Setup(x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        user))
                    .ReturnsAsync(package);

                // Act
                var result = await controller.CreatePackagePut();

                // Assert
                if (isPushAllowed)
                {
                    controller.MockPackageUploadService.Verify(
                        x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            user));
                }
                else
                {
                    controller.MockPackageUploadService.Verify(
                        x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>()),
                        Times.Never);

                    ResultAssert.IsStatusCode(
                        result,
                        HttpStatusCode.Unauthorized,
                        Strings.ApiKeyNotAuthorized);
                }
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

            [Fact]
            public async Task WillNotDeleteThePackageIfApiKeyDoesNotBelongToAnOwner()
            {
                var notOwner = new User { Key = 1 };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Owners = new[] { new User() } }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(notOwner);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict("theId", "1.0.42")).Returns(package);

                var result = await controller.DeletePackage("theId", "1.0.42");

                Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                Assert.Equal(string.Format(Strings.ApiKeyNotAuthorized, "delete"), statusCodeResult.StatusDescription);

                controller.MockPackageService.Verify(x => x.MarkPackageUnlistedAsync(package, true), Times.Never());
            }

            [InlineData("[{\"a\":\"all\", \"s\":\"*\"}]", true)]
            [InlineData("[{\"a\":\"package:unlist\", \"s\":\"theId\"}]", true)]
            [InlineData("[{\"a\":\"package:push\", \"s\":\"theId\"}]", false)]
            [Theory]
            public async Task WillVerifyApiKeyScopeBeforeDelete(string apiKeyScope, bool isDeleteAllowed)
            {
                var owner = new User { Key = 1, Username = "owner" };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration {
                        Id = "theId",
                        Owners = new[] { owner }
                    }
                };

                var credential = TestCredentialHelper.CreateV4ApiKey(expiration: null, plaintextApiKey: out string plaintextApiKey);
                credential.Scopes = JsonConvert.DeserializeObject<List<Scope>>(apiKeyScope);

                var controller = new TestableApiController(GetConfigurationService());
                controller.SetCurrentUser(owner, credential);
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict("theId", "1.0.42"))
                    .Returns(package);

                var result = await controller.DeletePackage("theId", "1.0.42");

                if (!isDeleteAllowed)
                {
                    Assert.IsType<HttpStatusCodeWithBodyResult>(result);
                    var statusCodeResult = (HttpStatusCodeWithBodyResult)result;
                    Assert.Equal(string.Format(Strings.ApiKeyNotAuthorized, "delete"),
                        statusCodeResult.StatusDescription);

                    controller.MockPackageService.Verify(x => x.MarkPackageUnlistedAsync(package, true), Times.Never());
                }
                else
                {
                    controller.MockPackageService.Verify(x => x.MarkPackageUnlistedAsync(package, true));
                    controller.MockIndexingService.Verify(i => i.UpdatePackage(package));
                }
            }

            [Fact]
            public async Task WillUnlistThePackageIfApiKeyBelongsToAnOwner()
            {
                var owner = new User { Key = 1 };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Owners = new[] { new User(), owner } }
                };
                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package);
                controller.SetCurrentUser(owner);

                ResultAssert.IsEmpty(await controller.DeletePackage("theId", "1.0.42"));

                controller.MockPackageService.Verify(x => x.MarkPackageUnlistedAsync(package, true));
                controller.MockIndexingService.Verify(i => i.UpdatePackage(package));
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

            [Fact]
            public async Task WillNotListThePackageIfApiKeyDoesNotBelongToAnOwner()
            {
                // Arrange
                var owner = new User { Key = 1 };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Owners = new[] { new User() } }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict("theId", "1.0.42")).Returns(package);
                controller.SetCurrentUser(owner);

                // Act
                var result = await controller.PublishPackage("theId", "1.0.42");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Forbidden,
                    String.Format(Strings.ApiKeyNotAuthorized, "publish"));

                controller.MockPackageService.Verify(x => x.MarkPackageListedAsync(package, It.IsAny<bool>()), Times.Never());
            }

            [Fact]
            public async Task WillListThePackageIfUserIsAnOwner()
            {
                // Arrange
                var owner = new User { Key = 1 };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Owners = new[] { new User(), owner } }
                };

                var controller = new TestableApiController(GetConfigurationService());
                controller.MockPackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(package);
                controller.SetCurrentUser(owner);

                // Act
                var result = await controller.PublishPackage("theId", "1.0.42");

                // Assert
                ResultAssert.IsEmpty(result);
                controller.MockPackageService.Verify(x => x.MarkPackageListedAsync(package, It.IsAny<bool>()));
                controller.MockIndexingService.Verify(i => i.UpdatePackage(package));
            }
        }

        public class PackageVerificationKeyContainer : TestContainer
        {
            internal TestableApiController SetupController(string keyType, string scopes, Package package, bool isOwner = true)
            {
                var fakes = Get<Fakes>();
                var user = fakes.User;
                var credential = user.Credentials.First(c => c.Type == keyType);

                if (!string.IsNullOrWhiteSpace(scopes))
                {
                    credential.Scopes = JsonConvert.DeserializeObject<List<Scope>>(scopes);
                }

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

                var id = package?.PackageRegistration?.Id ?? "foo";
                var version = package?.Version ?? "1.0.0";
                controller.MockPackageService
                    .Setup(s => s.FindPackageByIdAndVersion(id, version, SemVerLevelKey.SemVer2, true))
                    .Returns(package);

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
                var tempScope = await InvokeAsync("");

                Assert.Null(tempScope.OwnerKey);
                Assert.Equal("foo", tempScope.Subject);
                Assert.Equal(NuGetScopes.PackageVerify, tempScope.AllowedAction);
            }

            [Theory]
            [InlineData("[{\"a\":\"package:push\", \"s\":\"foo\"}]")]
            [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"foo\"}]")]
            public async Task WhenApiKeyHasNoOwnerScope_TempKeyHasScopeWithNoOwner(string scope)
            {
                var tempScope = await InvokeAsync(scope);

                Assert.Null(tempScope.OwnerKey);
                Assert.Equal("foo", tempScope.Subject);
                Assert.Equal(NuGetScopes.PackageVerify, tempScope.AllowedAction);
            }

            [Theory]
            [InlineData("[{\"o\": \"1234\", \"a\":\"package:push\", \"s\":\"foo\"}]")]
            [InlineData("[{\"o\": \"1234\", \"a\":\"package:pushversion\", \"s\":\"foo\"}]")]
            public async Task WhenApiKeyHasOwnerScope_TempKeyHasSameOwner(string scope)
            {
                var tempScope = await InvokeAsync(scope);

                Assert.Equal(1234, tempScope.OwnerKey);
                Assert.Equal("foo", tempScope.Subject);
                Assert.Equal(NuGetScopes.PackageVerify, tempScope.AllowedAction);
            }

            private async Task<Scope> InvokeAsync(string scope)
            {
                // Arrange
                var controller = SetupController(CredentialTypes.ApiKey.V4, scope, package: null);

                // Act
                var jsonResult = await controller.CreatePackageVerificationKeyAsync("foo", "1.0.0") as JsonResult;

                // Assert - the response
                dynamic json = jsonResult?.Data;
                Assert.NotNull(json);

                Guid key;
                Assert.True(Guid.TryParse(json.Key, out key));

                DateTime expires;
                Assert.True(DateTime.TryParse(json.Expires, out expires));

                // Assert - the invocations
                controller.MockAuthenticationService.Verify(s => s.AddCredential(It.IsAny<User>(), It.IsAny<Credential>()), Times.Once);

                controller.MockTelemetryService.Verify(x => x.TrackCreatePackageVerificationKeyEvent("foo", "1.0.0",
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
            [Fact]
            public async Task VerifyPackageKeyAsync_Returns400IfSecurityPolicyFails()
            {
                // Arrange
                var controller = SetupController(CredentialTypes.ApiKey.V4, "", package: null);
                controller.MockSecurityPolicyService.Setup(s => s.EvaluateAsync(It.IsAny<SecurityPolicyAction>(), It.IsAny<HttpContextBase>()))
                    .Returns(Task.FromResult(SecurityPolicyResult.CreateErrorResult("A")));

                // Act
                var result = await controller.VerifyPackageKeyAsync("foo", "1.0.0");

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.BadRequest, "A");
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1, "")]
            [InlineData(CredentialTypes.ApiKey.V2, "[{\"a\":\"package:push\", \"s\":\"foo\"}]")]
            [InlineData(CredentialTypes.ApiKey.V4, "[{\"a\":\"package:pushversion\", \"s\":\"foo\"}]")]
            public async Task VerifyPackageKeyAsync_Returns404IfPackageDoesNotExist_ApiKey(string apiKeyType, string scope)
            {
                // Arrange
                var controller = SetupController(apiKeyType, scope, package: null);

                // Act
                var result = await controller.VerifyPackageKeyAsync("foo", "1.0.0");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.NotFound,
                    String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, "foo", "1.0.0"));

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()), Times.Never);

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent("foo", "1.0.0",
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 404), Times.Once);
            }

            [Theory]
            [InlineData("[{\"a\":\"package:verify\", \"s\":\"foo\"}]")]
            public async Task VerifyPackageKeyAsync_Returns404IfPackageDoesNotExist_ApiKeyVerifyV1(string scope)
            {
                // Arrange
                var controller = SetupController(CredentialTypes.ApiKey.VerifyV1, scope, package: null);

                // Act
                var result = await controller.VerifyPackageKeyAsync("foo", "1.0.0");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.NotFound,
                    String.Format(CultureInfo.CurrentCulture, Strings.PackageWithIdAndVersionNotFound, "foo", "1.0.0"));

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()), Times.Once);

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent("foo", "1.0.0",
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 404), Times.Once);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1, "")]
            [InlineData(CredentialTypes.ApiKey.V2, "[{\"a\":\"package:push\", \"s\":\"foo\"}]")]
            [InlineData(CredentialTypes.ApiKey.V4, "[{\"a\":\"package:pushversion\", \"s\":\"foo\"}]")]
            public async Task VerifyPackageKeyAsync_Returns403IfUserIsNotAnOwner_ApiKey(string apiKeyType, string scope)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration() { Id = "foo" },
                    Version = "1.0.0"
                };
                var controller = SetupController(apiKeyType, scope, package, isOwner: false);

                // Act
                var result = await controller.VerifyPackageKeyAsync("foo", "1.0.0");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Forbidden,
                    Strings.ApiKeyNotAuthorized);

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()), Times.Never);

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent("foo", "1.0.0",
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 403), Times.Once);
            }

            [Theory]
            [InlineData("[{\"a\":\"package:verify\", \"s\":\"foo\"}]")]
            public async Task VerifyPackageKeyAsync_Returns403IfUserIsNotAnOwner_ApiKeyVerifyV1(string scope)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration() { Id = "foo" },
                    Version = "1.0.0"
                };
                var controller = SetupController(CredentialTypes.ApiKey.VerifyV1, scope, package, isOwner: false);

                // Act
                var result = await controller.VerifyPackageKeyAsync("foo", "1.0.0");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Forbidden,
                    Strings.ApiKeyNotAuthorized);

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()), Times.Once);

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent("foo", "1.0.0",
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 403), Times.Once);
            }

            [Theory]
            // action mismatch
            [InlineData(CredentialTypes.ApiKey.V2, "[{\"a\":\"package:unlist\", \"s\":\"foo\"}]")]
            [InlineData(CredentialTypes.ApiKey.V4, "[{\"a\":\"package:verify\", \"s\":\"foo\"}]")]
            // subject mismatch
            [InlineData(CredentialTypes.ApiKey.V2, "[{\"a\":\"package:push\", \"s\":\"notfoo\"}]")]
            [InlineData(CredentialTypes.ApiKey.V4, "[{\"a\":\"package:pushversion\", \"s\":\"notfoo\"}]")]
            public async Task VerifyPackageKeyAsync_Returns403IfScopeDoesNotMatch_ApiKey(string apiKeyType, string scope)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration() { Id = "foo" },
                    Version = "1.0.0"
                };
                var controller = SetupController(apiKeyType, scope, package);

                // Act
                var result = await controller.VerifyPackageKeyAsync("foo", "1.0.0");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Forbidden,
                    Strings.ApiKeyNotAuthorized);

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()), Times.Never);

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent("foo", "1.0.0",
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 403), Times.Once);
            }

            [Theory]
            // action mismatch
            [InlineData("[{\"a\":\"package:push\", \"s\":\"foo\"}]")]
            [InlineData("[{\"a\":\"package:pushversion\", \"s\":\"foo\"}]")]
            [InlineData("[{\"a\":\"package:unlist\", \"s\":\"foo\"}]")]
            // subject mismatch
            [InlineData("[{\"a\":\"package:verify\", \"s\":\"notfoo\"}]")]
            public async Task VerifyPackageKeyAsync_Returns403IfScopeDoesNotMatch_ApiKeyVerifyV1(string scope)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration() { Id = "foo" },
                    Version = "1.0.0"
                };
                var controller = SetupController(CredentialTypes.ApiKey.VerifyV1, scope, package);

                // Act
                var result = await controller.VerifyPackageKeyAsync("foo", "1.0.0");

                // Assert
                ResultAssert.IsStatusCode(
                    result,
                    HttpStatusCode.Forbidden,
                    Strings.ApiKeyNotAuthorized);

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()), Times.Once);

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent("foo", "1.0.0",
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 403), Times.Once);
            }

            [Theory]
            [InlineData(CredentialTypes.ApiKey.V1, "")]
            [InlineData(CredentialTypes.ApiKey.V2, "[{\"a\":\"package:push\", \"s\":\"foo\"}]")]
            [InlineData(CredentialTypes.ApiKey.V4, "[{\"a\":\"package:pushversion\", \"s\":\"foo\"}]")]
            public async Task VerifyPackageKeyAsync_Returns200IfApiKeyWithPushCapability_ApiKey(string apiKeyType, string scope)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration() { Id = "foo" },
                    Version = "1.0.0"
                };
                var controller = SetupController(apiKeyType, scope, package);

                // Act
                var result = await controller.VerifyPackageKeyAsync("foo", "1.0.0");

                // Assert
                ResultAssert.IsEmpty(result);

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()), Times.Never);

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent("foo", "1.0.0",
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 200), Times.Once);
            }

            [Theory]
            [InlineData("[{\"a\":\"package:verify\", \"s\":\"foo\"}]")]
            public async Task VerifyPackageKeyAsync_Returns200IfPackageVerifyKey_ApiKeyVerifyV1(string scope)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration() { Id = "foo" },
                    Version = "1.0.0"
                };
                var controller = SetupController(CredentialTypes.ApiKey.VerifyV1, scope, package);

                // Act
                var result = await controller.VerifyPackageKeyAsync("foo", "1.0.0");

                // Assert
                ResultAssert.IsEmpty(result);

                controller.MockAuthenticationService.Verify(s => s.RemoveCredential(It.IsAny<User>(), It.IsAny<Credential>()), Times.Once);

                controller.MockTelemetryService.Verify(x => x.TrackVerifyPackageKeyEvent("foo", "1.0.0",
                    It.IsAny<User>(), controller.OwinContext.Request.User.Identity, 200), Times.Once);
            }

            [Fact]
            public async Task VerifyPackageKeyAsync_WritesAuditRecord()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration() { Id = "foo" },
                    Version = "1.0.0"
                };
                var controller = SetupController(CredentialTypes.ApiKey.V4, "", package);

                // Act
                var result = await controller.VerifyPackageKeyAsync("foo", "1.0.0");

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

                HttpStatusCodeResult httpStatusResult = (HttpStatusCodeResult)actionResult;

                Assert.True(httpStatusResult.StatusCode == (int)HttpStatusCode.NotFound, "unexpected StatusCode");
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
