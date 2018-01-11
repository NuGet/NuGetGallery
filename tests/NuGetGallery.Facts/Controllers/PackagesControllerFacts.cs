// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet.Packaging;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.AsyncFileUpload;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Framework;
using NuGetGallery.Helpers;
using NuGetGallery.Packaging;
using NuGetGallery.Security;
using Xunit;
using System.Globalization;

namespace NuGetGallery
{
    public class PackagesControllerFacts
        : TestContainer
    {
        private static PackagesController CreateController(
            IGalleryConfigurationService configurationService,
            Mock<IPackageService> packageService = null,
            Mock<IUploadFileService> uploadFileService = null,
            Mock<IUserService> userService = null,
            Mock<IMessageService> messageService = null,
            Mock<HttpContextBase> httpContext = null,
            Stream fakeNuGetPackage = null,
            Mock<ISearchService> searchService = null,
            Exception readPackageException = null,
            Mock<IAutomaticallyCuratePackageCommand> autoCuratePackageCmd = null,
            Mock<IPackageFileService> packageFileService = null,
            Mock<IEntitiesContext> entitiesContext = null,
            Mock<IIndexingService> indexingService = null,
            Mock<ICacheService> cacheService = null,
            Mock<IPackageDeleteService> packageDeleteService = null,
            Mock<ISupportRequestService> supportRequestService = null,
            IAuditingService auditingService = null,
            Mock<ITelemetryService> telemetryService = null,
            Mock<ISecurityPolicyService> securityPolicyService = null,
            Mock<IReservedNamespaceService> reservedNamespaceService = null,
            Mock<IPackageUploadService> packageUploadService = null,
            Mock<IValidationService> validationService = null,
            Mock<IPackageOwnershipManagementService> packageOwnershipManagementService = null)
        {
            packageService = packageService ?? new Mock<IPackageService>();
            if (uploadFileService == null)
            {
                uploadFileService = new Mock<IUploadFileService>();
                uploadFileService.Setup(x => x.DeleteUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(0));
                uploadFileService.Setup(x => x.GetUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult<Stream>(null));
                uploadFileService.Setup(x => x.SaveUploadFileAsync(It.IsAny<int>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));
            }
            userService = userService ?? new Mock<IUserService>();
            messageService = messageService ?? new Mock<IMessageService>();
            searchService = searchService ?? CreateSearchService();
            autoCuratePackageCmd = autoCuratePackageCmd ?? new Mock<IAutomaticallyCuratePackageCommand>();

            if (packageFileService == null)
            {
                packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));
            }

            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();

            indexingService = indexingService ?? new Mock<IIndexingService>();

            cacheService = cacheService ?? new Mock<ICacheService>();
            
            packageDeleteService = packageDeleteService ?? new Mock<IPackageDeleteService>();

            supportRequestService = supportRequestService ?? new Mock<ISupportRequestService>();

            auditingService = auditingService ?? new TestAuditingService();

            telemetryService = telemetryService ?? new Mock<ITelemetryService>();

            securityPolicyService = securityPolicyService ?? new Mock<ISecurityPolicyService>();

            if (reservedNamespaceService == null)
            {
                reservedNamespaceService = new Mock<IReservedNamespaceService>();
                IReadOnlyCollection<ReservedNamespace> userOwnedMatchingNamespaces = new List<ReservedNamespace>();
                reservedNamespaceService.Setup(s => s.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(new ReservedNamespace[0]);
            }

            packageUploadService = packageUploadService ?? new Mock<IPackageUploadService>();

            validationService = validationService ?? new Mock<IValidationService>();

            packageOwnershipManagementService = packageOwnershipManagementService ?? new Mock<IPackageOwnershipManagementService>();

            var controller = new Mock<PackagesController>(
                packageService.Object,
                uploadFileService.Object,
                userService.Object,
                messageService.Object,
                searchService.Object,
                autoCuratePackageCmd.Object,
                packageFileService.Object,
                entitiesContext.Object,
                configurationService.Current,
                indexingService.Object,
                cacheService.Object,
                packageDeleteService.Object,
                supportRequestService.Object,
                auditingService,
                telemetryService.Object,
                securityPolicyService.Object,
                reservedNamespaceService.Object,
                packageUploadService.Object,
                new ReadMeService(packageFileService.Object, entitiesContext.Object),
                validationService.Object,
                packageOwnershipManagementService.Object);

            controller.CallBase = true;
            controller.Object.SetOwinContextOverride(Fakes.CreateOwinContext());

            httpContext = httpContext ?? new Mock<HttpContextBase>();
            TestUtility.SetupHttpContextMockForUrlGeneration(httpContext, controller.Object);

            if (readPackageException != null)
            {
                controller.Setup(x => x.CreatePackage(It.IsAny<Stream>())).Throws(readPackageException);
            }
            else
            {
                if (fakeNuGetPackage == null)
                {
                    fakeNuGetPackage = TestPackage.CreateTestPackageStream("thePackageId", "1.0.0");
                }

                controller.Setup(x => x.CreatePackage(It.IsAny<Stream>())).Returns(new PackageArchiveReader(fakeNuGetPackage, true));
            }

            return controller.Object;
        }

        private static Mock<ISearchService> CreateSearchService()
        {
            var searchService = new Mock<ISearchService>();
            searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns(
                (IQueryable<Package> p, string searchTerm) => Task.FromResult(new SearchResults(p.Count(), DateTime.UtcNow, p)));

            return searchService;
        }

        public class TheCancelVerifyPackageAction
            : TestContainer
        {
            [Fact]
            public async Task DeletesTheInProgressPackageUpload()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.CancelUpload();

                fakeUploadFileService.Verify(x => x.DeleteUploadFileAsync(42));
            }

            [Fact]
            public async Task RedirectsToUploadPageAfterDelete()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.CancelUpload();

                Assert.IsType<JsonResult>(result);
                Assert.Null(result.Data);
            }
        }

        public class TheDisplayPackageMethod
            : TestContainer
        {
            [Fact]
            public async Task GivenANonNormalizedVersionIt302sToTheNormalizedVersion()
            {
                // Arrange
                var controller = CreateController(GetConfigurationService());

                // Act
                var result = await controller.DisplayPackage("Foo", "01.01.01");

                // Assert
                ResultAssert.IsRedirectToRoute(result, new
                {
                    action = "DisplayPackage",
                    id = "Foo",
                    version = "1.1.1"
                }, permanent: true);
            }

            [Fact]
            public async Task GivenANonExistentPackageIt404s()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                packageService.Setup(p => p.FindPackageByIdAndVersion("Foo", "1.1.1", SemVerLevelKey.SemVer2, true))
                              .ReturnsNull();

                // Act
                var result = await controller.DisplayPackage("Foo", "1.1.1");

                // Assert
                ResultAssert.IsNotFound(result);
            }

            public static IEnumerable<PackageStatus> ValidatingPackageStatuses = 
                new[] { PackageStatus.Validating , PackageStatus.FailedValidation};

            public static IEnumerable<object[]> GivenAValidatingPackage_Data => ValidatingPackageStatuses.Select(s => new object[] { s });

            [Theory]
            [MemberData(nameof(GivenAValidatingPackage_Data))]
            public async Task GivenAValidatingPackageThatTheCurrentUserOwnsThenShowIt(PackageStatus packageStatus)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    TestUtility.FakeUser,
                    TestUtility.FakeUser);

                // Assert
                Assert.IsType<ViewResult>(result);
            }

            [Theory]
            [MemberData(nameof(GivenAValidatingPackage_Data))]
            public async Task GivenAValidatingPackageAsAdminThenShowIt(PackageStatus packageStatus)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    TestUtility.FakeAdminUser,
                    new User { Key = 132114 });

                // Assert
                Assert.IsType<ViewResult>(result);
            }

            public static IEnumerable<object[]> GivenAValidatingPackageThatCurrentUsersOrganizationOwnsThenShowIt_Data
            {
                get
                {
                    foreach (var isAdmin in new[] { true, false })
                    {
                        foreach (var status in ValidatingPackageStatuses)
                        {
                            yield return new object[]
                            {
                                status,
                                isAdmin
                            };
                        }
                    }
                }
            }

            [Theory]
            [MemberData(nameof(GivenAValidatingPackageThatCurrentUsersOrganizationOwnsThenShowIt_Data))]
            public async Task GivenAValidatingPackageThatCurrentUsersOrganizationOwnsThenShowIt(PackageStatus packageStatus, bool isAdmin)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    isAdmin ? TestUtility.FakeOrganizationAdmin : TestUtility.FakeOrganizationCollaborator,
                    TestUtility.FakeOrganization);

                // Assert
                Assert.IsType<ViewResult>(result);
            }

            [Theory]
            [MemberData(nameof(GivenAValidatingPackage_Data))]
            public async Task GivenAValidatingPackageWhileLoggedOutThenHideIt(PackageStatus packageStatus)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    null,
                    new User { Key = 132114 });

                // Assert
                ResultAssert.IsNotFound(result);
            }

            [Theory]
            [MemberData(nameof(GivenAValidatingPackage_Data))]
            public async Task GivenAValidatingPackageThatTheCurrentUserDoesNotOwnThenHideIt(PackageStatus packageStatus)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    TestUtility.FakeUser,
                    new User { Key = 132114 });

                // Assert
                ResultAssert.IsNotFound(result);
            }

            private async Task<ActionResult> GetActionResultForPackageStatusAsync(
                PackageStatus packageStatus,
                User currentUser,
                User owner)
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var httpContext = new Mock<HttpContextBase>();
                var httpCachePolicy = new Mock<HttpCachePolicyBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    httpContext: httpContext);
                controller.SetCurrentUser(currentUser);

                httpContext.Setup(c => c.Response.Cache).Returns(httpCachePolicy.Object);

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "NuGet.Versioning",
                        Owners = new[] { owner }
                    },
                    Version = "3.4.0",
                    NormalizedVersion = "3.4.0",
                    PackageStatusKey = packageStatus,
                };

                packageService
                    .Setup(p => p.FindPackageByIdAndVersion(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int?>(),
                        It.IsAny<bool>()))
                    .Returns(package);

                // Act
                var result = await controller.DisplayPackage(
                    package.PackageRegistration.Id,
                    package.NormalizedVersion);

                return result;
            }

            public static IEnumerable<object[]> PackageOwners
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        new User { Key = 12414 }
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            public static IEnumerable<object[]> PackageNonOwners
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 12414 }
                    };

                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };
                }
            }

            [Theory]
            [MemberData(nameof(PackageOwners))]
            public Task GivenAnOwnedValidPackageWithNoEditsItDisplaysCurrentMetadata(User currentUser, User owner)
            {
                return CheckValidPackage(currentUser, owner);
            }

            [Theory]
            [MemberData(nameof(PackageNonOwners))]
            public Task GivenAnUnownedValidPackageWithNoEditsItDisplaysCurrentMetadata(User currentUser, User owner)
            {
                return CheckValidPackage(currentUser, owner);
            }

            private async Task CheckValidPackage(User currentUser, User owner)
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var httpContext = new Mock<HttpContextBase>();
                var httpCachePolicy = new Mock<HttpCachePolicyBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService,
                    httpContext: httpContext);
                controller.SetCurrentUser(currentUser);
                httpContext.Setup(c => c.Response.Cache).Returns(httpCachePolicy.Object);
                var title = "A test package!";
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo",
                        Owners = new List<User>() { owner }
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = title
                };

                packageService
                    .Setup(p => p.FindPackageByIdAndVersion("Foo", "1.1.1", SemVerLevelKey.SemVer2, true))
                    .Returns(package);
                                
                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                // Act
                var result = await controller.DisplayPackage("Foo", "1.1.1");

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("1.1.1", model.Version);
                Assert.Equal(title, model.Title);
            }

            [Fact]
            public async Task GivenAnAbsoluteLatestVersionItQueriesTheCorrectVersion()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService, indexingService: indexingService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                packageService
                     .Setup(p => p.FindAbsoluteLatestPackageById("Foo", SemVerLevelKey.SemVer2))
                     .Returns(new Package()
                     {
                         PackageRegistration = new PackageRegistration()
                         {
                             Id = "Foo",
                             Owners = new List<User>()
                         },
                         Version = "2.0.0",
                         NormalizedVersion = "2.0.0",
                         IsLatest = true,
                         Title = "A test package!"
                     });


                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                // Act
                var result = await controller.DisplayPackage("Foo", Constants.AbsoluteLatestUrlString);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("2.0.0", model.Version);
                Assert.Equal("A test package!", model.Title);
                Assert.True(model.LatestVersion);
            }

            [Fact]
            public async Task GivenAValidPackageWithNoVersionThatTheCurrentUserDoesNotOwnItDisplaysCurrentMetadata()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                packageService.Setup(p => p.FindPackageByIdAndVersion("Foo", null, SemVerLevelKey.SemVer2, true))
                    .Returns(new Package()
                    {
                        PackageRegistration = new PackageRegistration()
                        {
                            Id = "Foo",
                            Owners = new List<User>()
                        },
                        Version = "01.1.01",
                        NormalizedVersion = "1.1.1",
                        Title = "A test package!"
                    });

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                // Act
                var result = await controller.DisplayPackage("Foo", null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("1.1.1", model.Version);
                Assert.Equal("A test package!", model.Title);
                Assert.Null(model.ReadMeHtml);
            }

            [Fact]
            public async Task WhenHasReadMeAndMarkdownExists_ReturnsContent()
            {
                // Arrange
                var readMeMd = "# Hello World!";

                // Act
                var result = await GetDisplayPackageResult(readMeMd, true);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("<h2>Hello World!</h2>", model.ReadMeHtml);
            }

            [Fact]
            public async Task WhenHasReadMeAndLongMarkdownExists_ReturnsClampedContent()
            {
                // Arrange
                var readMeMd = string.Concat(Enumerable.Repeat($"---{Environment.NewLine}", 20));

                // Act
                var result = await GetDisplayPackageResult(readMeMd, true);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);

                var htmlCount = model.ReadMeHtml.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                Assert.Equal(20, htmlCount);
            }

            [Fact]
            public async Task WhenHasReadMeAndFileNotFound_ReturnsNull()
            {
                // Arrange & Act
                var result = await GetDisplayPackageResult(null, true);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Null(model.ReadMeHtml);
            }

            [Fact]
            public async Task WhenHasReadMeFalse_ReturnsNull()
            {
                // Arrange and Act
                var result = await GetDisplayPackageResult(null, false);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Null(model.ReadMeHtml);
            }

            private async Task<ActionResult> GetDisplayPackageResult(string readMeHtml, bool hasReadMe)
            {
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var fileService = new Mock<IPackageFileService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService, indexingService: indexingService, packageFileService: fileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo",
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!",
                    HasReadMe = hasReadMe
                };

                packageService.Setup(p => p.FindPackageByIdAndVersion(It.Is<string>(s => s == "Foo"), It.Is<string>(s => s == null), It.Is<int>(i => i == SemVerLevelKey.SemVer2), It.Is<bool>(b => b == true)))
                    .Returns(package);

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                if (hasReadMe)
                {
                    fileService.Setup(f => f.DownloadReadMeMdFileAsync(It.IsAny<Package>())).Returns(Task.FromResult(readMeHtml));
                }

                return await controller.DisplayPackage("Foo", /*version*/null);
            }

            [Fact]
            public async Task GetsValidationIssues()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var fileService = new Mock<IPackageFileService>();
                var validationService = new Mock<IValidationService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService, indexingService: indexingService, packageFileService: fileService, validationService: validationService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo",
                        Owners = new List<User>()
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!",
                };

                packageService.Setup(p => p.FindPackageByIdAndVersion(
                                                It.Is<string>(s => s == "Foo"),
                                                It.Is<string>(s => s == null),
                                                It.Is<int>(i => i == SemVerLevelKey.SemVer2),
                                                It.Is<bool>(b => b == true)))
                    .Returns(package);

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                var expectedIssues = new[]
                {
                    new TestIssue("This should not be deduplicated by the controller layer"),
                    new TestIssue("I'm a Teapot"),
                    new TestIssue("This should not be deduplicated by the controller layer"),
                };

                validationService.Setup(v => v.GetLatestValidationIssues(It.IsAny<Package>()))
                    .Returns(expectedIssues);

                // Act
                var result = await controller.DisplayPackage("Foo", version: null);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal(model.ValidationIssues, expectedIssues);
            }

            private class TestIssue : ValidationIssue
            {
                private readonly string _message;

                public TestIssue(string message) => _message = message;

                public override ValidationIssueCode IssueCode => throw new NotImplementedException();
            }
        }

        public class TheOwnershipRequestMethods : TestContainer
        {
            private int _key = 0;

            public delegate Task<ActionResult> InvokeOwnershipRequest(PackagesController packagesController, string id, string username, string token);

            private static Task<ActionResult> ConfirmOwnershipRequest(PackagesController packagesController, string id, string username, string token)
            {
                return packagesController.ConfirmPendingOwnershipRequest(id, username, token);
            }

            private static Task<ActionResult> RejectOwnershipRequest(PackagesController packagesController, string id, string username, string token)
            {
                return packagesController.RejectPendingOwnershipRequest(id, username, token);
            }

            public static IEnumerable<object[]> TheOwnershipRequestMethods_Data
            {
                get
                {
                    yield return new object[] { new InvokeOwnershipRequest(ConfirmOwnershipRequest) };
                    yield return new object[] { new InvokeOwnershipRequest(RejectOwnershipRequest) };
                }
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public async Task WithEmptyTokenReturnsHttpNotFound(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(new PackageRegistration());
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(new User { Username = "username" });

                // Act
                var result = await invokeOwnershipRequest(controller, "foo", "username", "");

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public async Task WithIdentityNotMatchingUserInRequestReturnsNotYourRequest(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                // Arrange
                var requestedUser = new User { Username = "userA", Key = _key++ };
                var currentUser = new User { Username = "userB", Key = _key++ };

                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(requestedUser.Username)).Returns(requestedUser);

                var controller = CreateController(GetConfigurationService(), userService: userService);
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await invokeOwnershipRequest(controller, "foo", requestedUser.Username, "token");

                // Assert
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                Assert.Equal(ConfirmOwnershipResult.NotYourRequest, model.Result);
                Assert.Equal(requestedUser.Username, model.Username);
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public Task WithSiteAdminReturnsNotYourRequest(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                return ReturnsNotYourRequest(TestUtility.FakeAdminUser, TestUtility.FakeUser, invokeOwnershipRequest);
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public Task WithOrganizationCollaboratorReturnsNotYourRequest(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                return ReturnsNotYourRequest(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization, invokeOwnershipRequest);
            }

            private async Task ReturnsNotYourRequest(User currentUser, User owner, InvokeOwnershipRequest invokeOwnershipRequest)
            {
                // Arrange
                var package = new PackageRegistration { Id = "foo" };

                var mockHttpContext = new Mock<HttpContextBase>();

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById(package.Id)).Returns(package);

                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(owner.Username)).Returns(owner);

                var controller = CreateController(
                    GetConfigurationService(),
                    httpContext: mockHttpContext,
                    packageService: packageService,
                    userService: userService);
                controller.SetCurrentUser(currentUser);
                TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                // Act
                var result = await invokeOwnershipRequest(controller, package.Id, owner.Username, "token");

                // Assert
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                Assert.Equal(ConfirmOwnershipResult.NotYourRequest, model.Result);
                Assert.Equal(owner.Username, model.Username);
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public async Task WithNonExistentPackageIdReturnsHttpNotFound(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                // Arrange
                var currentUser = new User { Username = "username", Key = _key++ };

                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(currentUser.Username)).Returns(currentUser);

                var controller = CreateController(GetConfigurationService(), userService: userService);
                controller.SetCurrentUser(currentUser);

                // Act
                var result = await invokeOwnershipRequest(controller, "foo", "username", "token");

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Theory]
            [MemberData(nameof(TheOwnershipRequestMethods_Data))]
            public async Task WithOwnerReturnsAlreadyOwnerResult(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                // Arrange
                var package = new PackageRegistration { Id = "foo" };
                var user = new User { Username = "username" };
                package.Owners.Add(user);
                var mockHttpContext = new Mock<HttpContextBase>();
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById(package.Id)).Returns(package);
                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(user.Username)).Returns(user);
                var controller = CreateController(
                    GetConfigurationService(),
                    httpContext: mockHttpContext,
                    packageService: packageService,
                    userService: userService);
                controller.SetCurrentUser(user);
                TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                // Act
                var result = await invokeOwnershipRequest(controller, package.Id, user.Username, "token");

                // Assert
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                Assert.Equal(ConfirmOwnershipResult.AlreadyOwner, model.Result);
            }

            public delegate Expression<Func<IPackageOwnershipManagementService, Task>> PackageOwnershipManagementServiceRequestExpression(PackageRegistration package, User user);

            private static Expression<Func<IPackageOwnershipManagementService, Task>> PackagesServiceForConfirmOwnershipRequestExpression(PackageRegistration package, User user)
            {
                return packageOwnershipManagementService => packageOwnershipManagementService.AddPackageOwnerAsync(package, user);
            }

            private static Expression<Func<IPackageOwnershipManagementService, Task>> PackagesServiceForRejectOwnershipRequestExpression(PackageRegistration package, User user)
            {
                return packageOwnershipManagementService => packageOwnershipManagementService.DeletePackageOwnershipRequestAsync(package, user);
            }

            public delegate Expression<Action<IMessageService>> MessageServiceForOwnershipRequestExpression(PackageOwnerRequest request);

            private static Expression<Action<IMessageService>> MessageServiceForConfirmOwnershipRequestExpression(PackageOwnerRequest request)
            {
                return messageService => messageService.SendPackageOwnerAddedNotice(
                    request.RequestingOwner,
                    request.NewOwner,
                    request.PackageRegistration,
                    It.IsAny<string>(), // The method that creates this URL correctly is not set up for these tests, so we cannot assert the expected value.
                    string.Empty);
            }

            private static Expression<Action<IMessageService>> MessageServiceForRejectOwnershipRequestExpression(PackageOwnerRequest request)
            {
                return messageService => messageService.SendPackageOwnerRequestRejectionNotice(request.RequestingOwner, request.NewOwner, request.PackageRegistration);
            }

            public static IEnumerable<object[]> ReturnsSuccessIfTokenIsValid_Data
            {
                get
                {
                    foreach (var tokenValid in new bool[] { true, false })
                    {
                        foreach (var isOrganizationAdministrator in new bool[] { true, false })
                        {
                            yield return new object[]
                            {
                                new InvokeOwnershipRequest(ConfirmOwnershipRequest),
                                new PackageOwnershipManagementServiceRequestExpression(PackagesServiceForConfirmOwnershipRequestExpression),
                                new MessageServiceForOwnershipRequestExpression(MessageServiceForConfirmOwnershipRequestExpression),
                                ConfirmOwnershipResult.Success,
                                tokenValid,
                                isOrganizationAdministrator
                            };
                            yield return new object[]
                            {
                                new InvokeOwnershipRequest(RejectOwnershipRequest),
                                new PackageOwnershipManagementServiceRequestExpression(PackagesServiceForRejectOwnershipRequestExpression),
                                new MessageServiceForOwnershipRequestExpression(MessageServiceForRejectOwnershipRequestExpression),
                                ConfirmOwnershipResult.Rejected,
                                tokenValid,
                                isOrganizationAdministrator
                            };
                        }
                    }
                }
            }

            [Theory]
            [MemberData("ReturnsSuccessIfTokenIsValid_Data")]
            public async Task ReturnsSuccessIfTokenIsValid(
                InvokeOwnershipRequest invokeOwnershipRequest, 
                PackageOwnershipManagementServiceRequestExpression packageOwnershipManagementServiceExpression, 
                MessageServiceForOwnershipRequestExpression messageServiceExpression, 
                ConfirmOwnershipResult successState, 
                bool tokenValid,
                bool isOrganizationAdministrator)
            {
                // Arrange
                var token = "token";
                var requestingOwner = new User { Key = _key++, Username = "owner" };
                var package = new PackageRegistration { Id = "foo", Owners = new[] { requestingOwner } };
                var currentUser = new User { Key = _key++, Username = "username" };

                User newOwner;
                if (isOrganizationAdministrator)
                {
                    newOwner = new Organization { Key = _key++, Username = "organization", Members = new[] { new Membership { Member = currentUser, IsAdmin = true } } };
                }
                else
                {
                    newOwner = currentUser;
                }

                var mockHttpContext = new Mock<HttpContextBase>();

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById(package.Id)).Returns(package);

                var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
                packageOwnershipManagementService.Setup(p => p.AddPackageOwnerAsync(package, newOwner)).Returns(Task.CompletedTask).Verifiable();
                packageOwnershipManagementService.Setup(p => p.DeletePackageOwnershipRequestAsync(package, newOwner)).Returns(Task.CompletedTask).Verifiable();

                var request = new PackageOwnerRequest
                {
                    PackageRegistration = package,
                    RequestingOwner = requestingOwner,
                    NewOwner = newOwner,
                    ConfirmationCode = token
                };
                packageOwnershipManagementService.Setup(p => p.GetPackageOwnershipRequest(package, newOwner, token))
                    .Returns(tokenValid ? request : null);

                var messageService = new Mock<IMessageService>();

                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(newOwner.Username)).Returns(newOwner);

                var controller = CreateController(
                    GetConfigurationService(),
                    httpContext: mockHttpContext,
                    packageService: packageService,
                    messageService: messageService,
                    packageOwnershipManagementService: packageOwnershipManagementService,
                    userService: userService);

                controller.SetCurrentUser(currentUser);
                TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                // Act
                var result = await invokeOwnershipRequest(controller, package.Id, newOwner.Username, token);

                // Assert
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                var expectedResult = tokenValid ? successState : ConfirmOwnershipResult.Failure;
                Assert.Equal(expectedResult, model.Result);
                Assert.Equal(package.Id, model.PackageId);
                packageOwnershipManagementService.Verify(packageOwnershipManagementServiceExpression(package, newOwner), tokenValid ? Times.Once() : Times.Never());
                messageService.Verify(messageServiceExpression(request), tokenValid ? Times.Once() : Times.Never());
            }

            public class TheCancelPendingOwnershipRequestMethod : TestContainer
            {
                [Fact]
                public async Task WithIdentityNotMatchingUserInRequestReturnsViewWithMessage()
                {
                    // Arrange
                    var controller = CreateController(GetConfigurationService());
                    controller.SetCurrentUser(new User("userA"));

                    // Act
                    var result = await controller.CancelPendingOwnershipRequest("foo", "userB", "userC");

                    // Assert
                    var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                    Assert.Equal(ConfirmOwnershipResult.NotYourRequest, model.Result);
                    Assert.Equal("userB", model.Username);
                }

                [Fact]
                public async Task WithNonExistentPackageIdReturnsHttpNotFound()
                {
                    // Arrange
                    var controller = CreateController(GetConfigurationService());
                    controller.SetCurrentUser(new User { Username = "userA" });

                    // Act
                    var result = await controller.CancelPendingOwnershipRequest("foo", "userA", "userB");

                    // Assert
                    Assert.IsType<HttpNotFoundResult>(result);
                }

                [Fact]
                public async Task WithNonExistentPendingUserReturnsHttpNotFound()
                {
                    // Arrange
                    var package = new PackageRegistration { Id = "foo" };
                    var user = new User { Username = "userA" };
                    var packageService = new Mock<IPackageService>();
                    packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(package);
                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: packageService);
                    controller.SetCurrentUser(user);

                    // Act
                    var result = await controller.CancelPendingOwnershipRequest("foo", "userA", "userB");

                    // Assert
                    Assert.IsType<HttpNotFoundResult>(result);
                }

                [Fact]
                public async Task WithNonExistentPackageOwnershipRequestReturnsHttpNotFound()
                {
                    // Arrange
                    var packageId = "foo";
                    var package = new PackageRegistration { Id = packageId };

                    var packageService = new Mock<IPackageService>();
                    packageService.Setup(p => p.FindPackageRegistrationById(packageId)).Returns(package);

                    var userAName = "userA";
                    var userA = new User { Username = userAName };

                    var userBName = "userB";
                    var userB = new User { Username = userBName };

                    var userService = new Mock<IUserService>();
                    userService.Setup(u => u.FindByUsername(userAName)).Returns(userA);
                    userService.Setup(u => u.FindByUsername(userBName)).Returns(userB);

                    var controller = CreateController(
                        GetConfigurationService(),
                        userService: userService,
                        packageService: packageService);
                    controller.SetCurrentUser(userA);

                    // Act
                    var result = await controller.CancelPendingOwnershipRequest(packageId, userAName, userBName);

                    // Assert
                    Assert.IsType<HttpNotFoundResult>(result);
                }

                [Fact]
                public async Task ReturnsCancelledIfPackageOwnershipRequestExists()
                {
                    // Arrange
                    var packageId = "foo";
                    var package = new PackageRegistration { Id = packageId };

                    var packageService = new Mock<IPackageService>();
                    packageService.Setup(p => p.FindPackageRegistrationById(packageId)).Returns(package);

                    var userAName = "userA";
                    var userA = new User { Username = userAName };

                    var userBName = "userB";
                    var userB = new User { Username = userBName };

                    var userService = new Mock<IUserService>();
                    userService.Setup(u => u.FindByUsername(userAName)).Returns(userA);
                    userService.Setup(u => u.FindByUsername(userBName)).Returns(userB);

                    var request = new PackageOwnerRequest() { RequestingOwner = userA, NewOwner = userB };
                    var packageOwnershipManagementRequestService = new Mock<IPackageOwnershipManagementService>();
                    packageOwnershipManagementRequestService.Setup(p => p.GetPackageOwnershipRequests(package, userA, userB)).Returns(new[] { request });
                    packageOwnershipManagementRequestService.Setup(p => p.DeletePackageOwnershipRequestAsync(package, userB)).Returns(Task.CompletedTask).Verifiable();

                    var messageService = new Mock<IMessageService>();

                    var controller = CreateController(
                        GetConfigurationService(),
                        userService: userService,
                        packageService: packageService,
                        packageOwnershipManagementService: packageOwnershipManagementRequestService,
                        messageService: messageService);
                    controller.SetCurrentUser(userA);

                    // Act
                    var result = await controller.CancelPendingOwnershipRequest(packageId, userAName, userBName);

                    // Assert
                    var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                    var expectedResult = ConfirmOwnershipResult.Cancelled;
                    Assert.Equal(expectedResult, model.Result);
                    Assert.Equal(packageId, model.PackageId);
                    packageService.Verify();
                    packageOwnershipManagementRequestService.Verify();
                    messageService.Verify(m => m.SendPackageOwnerRequestCancellationNotice(userA, userB, package));
                }
            }

            public class TheConfirmOwnerMethod_SecurePushPropagation : TestContainer
            {
                [Fact]
                public async Task SubscribesOwnersToSecurePushAndSendsEmailIfNewOwnerRequires()
                {
                    // Arrange
                    var fakes = Get<Fakes>();
                    fakes.Package.Owners.Add(fakes.ShaUser);
                    fakes.User.SecurityPolicies = new RequireSecurePushForCoOwnersPolicy().Policies.ToList();

                    Assert.Equal(0, fakes.Owner.SecurityPolicies.Count);

                    // Act & Assert
                    var policyMessages = await AssertConfirmOwnerSubscribesUser(fakes, fakes.Owner, fakes.ShaUser, fakes.OrganizationOwner);
                    Assert.Equal(4, policyMessages.Count);

                    // subscribed notification
                    Assert.StartsWith("Owner(s) 'testUser' has (have) the following requirements that are now enforced for your account:",
                        policyMessages[fakes.Owner.Username]);
                    Assert.StartsWith("Owner(s) 'testUser' has (have) the following requirements that are now enforced for your account:",
                        policyMessages[fakes.ShaUser.Username]);
                    Assert.StartsWith("Owner(s) 'testUser' has (have) the following requirements that are now enforced for your account:",
                        policyMessages[fakes.OrganizationOwner.Username]);

                    // propagator notification
                    Assert.StartsWith("Owner(s) 'testUser' has (have) the following requirements that are now enforced for co-owner(s) '",
                        policyMessages[fakes.User.Username]);
                }

                [Fact]
                public async Task SubscribesNewOwnerToSecurePushAndSendsEmailIfOwnerRequires()
                {
                    // Arrange
                    var fakes = Get<Fakes>();
                    fakes.Package.Owners.Add(fakes.ShaUser);
                    fakes.Owner.SecurityPolicies = new RequireSecurePushForCoOwnersPolicy().Policies.ToList();

                    Assert.Equal(0, fakes.User.SecurityPolicies.Count);

                    // Act & Assert
                    var policyMessages = await AssertConfirmOwnerSubscribesUser(fakes, fakes.User);

                    Assert.False(policyMessages.ContainsKey(fakes.User.Username));
                    Assert.Equal(3, policyMessages.Count);
                    Assert.StartsWith("Owner(s) 'testPackageOwner' has (have) the following requirements that are now enforced for co-owner(s) 'testUser':",
                        policyMessages[fakes.Owner.Username]);
                    Assert.Equal("", policyMessages[fakes.ShaUser.Username]);
                    Assert.Equal("", policyMessages[fakes.OrganizationOwner.Username]);
                }

                private async Task<IDictionary<string, string>> AssertConfirmOwnerSubscribesUser(Fakes fakes, params User[] usersSubscribed)
                {
                    // Arrange
                    var mockHttpContext = new Mock<HttpContextBase>();

                    var packageService = new Mock<IPackageService>();
                    packageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>())).Returns(fakes.Package);

                    var packageOwnershipManagementService = new Mock<IPackageOwnershipManagementService>();
                    packageOwnershipManagementService.Setup(p => p.GetPackageOwnershipRequest(fakes.Package, fakes.User, "token")).Returns(
                        new PackageOwnerRequest
                        {
                            PackageRegistration = fakes.Package,
                            NewOwner = fakes.User,
                            ConfirmationCode = "token"
                        });

                    var policyService = new Mock<ISecurityPolicyService>();
                    foreach (var user in usersSubscribed)
                    {
                        policyService.Setup(s => s.SubscribeAsync(user, "SecurePush"))
                            .Returns(Task.FromResult(true))
                            .Verifiable();
                    }

                    var userService = new Mock<IUserService>();
                    userService.Setup(x => x.FindByUsername(fakes.User.Username)).Returns(fakes.User);

                    var policyMessages = new Dictionary<string, string>();
                    var messageService = new Mock<IMessageService>();
                    messageService.Setup(s => s.SendPackageOwnerAddedNotice(
                        It.IsAny<User>(), It.IsAny<User>(), It.IsAny<PackageRegistration>(), It.IsAny<string>(), It.IsAny<string>()))
                        .Callback<User, User, PackageRegistration, string, string>((toUser, newOwner, pkg, pkgUrl, policyMessage) =>
                        {
                            policyMessages.Add(toUser.Username, policyMessage);
                        });

                    var controller = CreateController(
                        GetConfigurationService(),
                        httpContext: mockHttpContext,
                        packageService: packageService,
                        packageOwnershipManagementService: packageOwnershipManagementService,
                        messageService: messageService,
                        securityPolicyService: policyService,
                        userService: userService);

                    controller.SetCurrentUser(fakes.User);
                    TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                    // Act
                    await controller.ConfirmPendingOwnershipRequest(fakes.Package.Id, fakes.User.Username, "token");

                    // Assert
                    foreach (var user in usersSubscribed)
                    {
                        policyService.Verify(s => s.SubscribeAsync(user, "SecurePush"), Times.Once);
                    }

                    return policyMessages;
                }
            }
        }

        public class TheContactOwnersMethod
            : TestContainer
        {
            [Fact]
            public void OnlyShowsOwnersWhoAllowReceivingEmails()
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "pkgid",
                        Owners = new[]
                            {
                                new User { Username = "helpful", EmailAllowed = true },
                                new User { Username = "grinch", EmailAllowed = false },
                                new User { Username = "helpful2", EmailAllowed = true }
                            }
                    }
                };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersion("pkgid", null, null, true)).Returns(package);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                var model = (controller.ContactOwners("pkgid") as ViewResult).Model as ContactOwnersViewModel;

                Assert.Equal(2, model.Owners.Count());
                Assert.Empty(model.Owners.Where(u => u.Username == "grinch"));
            }

            [Fact]
            public void HtmlEncodesMessageContent()
            {
                var sentPackageUrl = string.Empty;
                var messageService = new Mock<IMessageService>();
                string sentMessage = null;
                messageService.Setup(
                    s => s.SendContactOwnersMessage(
                        It.IsAny<MailAddress>(),
                        It.IsAny<PackageRegistration>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        false))
                    .Callback<MailAddress, PackageRegistration, string, string, string, bool>((_, __, packageUrl, msg, ____, _____) =>
                    {
                        sentPackageUrl = packageUrl;
                        sentMessage = msg;
                    });
                var package = new PackageRegistration { Id = "factory" };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("factory")).Returns(package);
                var userService = new Mock<IUserService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    messageService: messageService);
                controller.SetCurrentUser(new User { EmailAddress = "montgomery@burns.example.com", Username = "Montgomery" });
                var model = new ContactOwnersViewModel
                {
                    Message = "I like the cut of your jib. It's <b>bold</b>.",
                };

                var result = controller.ContactOwners("factory", model) as RedirectToRouteResult;

                Assert.Equal("I like the cut of your jib. It&#39;s &lt;b&gt;bold&lt;/b&gt;.", sentMessage);
                Assert.Equal(controller.Url.Package(package, false), sentPackageUrl);
            }

            [Fact]
            public void CallsSendContactOwnersMessageWithUserInfo()
            {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(
                    s => s.SendContactOwnersMessage(
                        It.IsAny<MailAddress>(),
                        It.IsAny<PackageRegistration>(),
                        It.IsAny<string>(),
                        "I like the cut of your jib",
                        It.IsAny<string>(), false));
                var package = new PackageRegistration { Id = "factory" };

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("factory")).Returns(package);
                var userService = new Mock<IUserService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    messageService: messageService);
                controller.SetCurrentUser(new User { EmailAddress = "montgomery@burns.example.com", Username = "Montgomery" });
                var model = new ContactOwnersViewModel
                {
                    Message = "I like the cut of your jib",
                };

                var result = controller.ContactOwners("factory", model) as RedirectToRouteResult;

                Assert.NotNull(result);
            }
        }

        public class TheDeleteMethod
            : TestContainer
        {
            private string _packageId = "CrestedGecko";
            private PackageRegistration _packageRegistration;
            private Package _package;

            public TheDeleteMethod()
            {
                _packageRegistration = new PackageRegistration { Id = _packageId };

                _package = new Package
                {
                    Key = 2,
                    PackageRegistration = _packageRegistration,
                    Version = "1.0.0+metadata",
                    Listed = true,
                    IsLatestSemVer2 = true,
                    HasReadMe = false
                };
                var olderPackageVersion = new Package
                {
                    Key = 1,
                    PackageRegistration = _packageRegistration,
                    Version = "1.0.0-alpha",
                    IsLatest = true,
                    IsLatestSemVer2 = true,
                    Listed = true,
                    HasReadMe = false
                };

                _packageRegistration.Packages.Add(_package);
                _packageRegistration.Packages.Add(olderPackageVersion);
            }

            [Fact]
            public void Returns404IfPackageNotFound()
            {
                var controller = CreateController(GetConfigurationService());

                var result = controller.Delete(_packageRegistration.Id, _package.Version);
                
                Assert.IsType<HttpNotFoundResult>(result);
            }

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 5535 }
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public void Returns401IfNotOwner(User currentUser, User owner)
            {
                var result = GetDeleteResult(currentUser, owner, out var controller);
                
                Assert.IsType<HttpStatusCodeResult>(result);
                var httpStatusCodeResult = result as HttpStatusCodeResult;
                Assert.Equal(401, httpStatusCodeResult.StatusCode);
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public void DisplaysFullVersionStringAndUsesNormalizedVersionsInUrlsInSelectList(User currentUser, User owner)
            {
                var result = GetDeleteResult(currentUser, owner, out var controller);

                Assert.IsType<ViewResult>(result);
                var model = ((ViewResult)result).Model as DeletePackageViewModel;
                Assert.NotNull(model);
                Assert.False(model.IsLocked);

                // Verify version select list
                Assert.Equal(_packageRegistration.Packages.Count, model.VersionSelectList.Count());

                foreach (var pkg in _packageRegistration.Packages)
                {
                    var valueField = UrlExtensions.DeletePackage(controller.Url, model);
                    var textField = model.NuGetVersion.ToFullString() + (pkg.IsLatestSemVer2 ? " (Latest)" : string.Empty);

                    var selectListItem = model.VersionSelectList
                        .SingleOrDefault(i => string.Equals(i.Text, textField) && string.Equals(i.Value, valueField));

                    Assert.NotNull(selectListItem);
                    Assert.Equal(valueField, selectListItem.Value);
                    Assert.Equal(textField, selectListItem.Text);
                }
            }

            [Fact]
            public void WhenPackageRegistrationIsLockedReturnsLockedState()
            {
                // Arrange
                var user = new User("Frodo") { Key = 1 };
                var packageRegistration = new PackageRegistration { Id = "Foo", IsLocked = true };
                packageRegistration.Owners.Add(user);

                var package = new Package
                {
                    Key = 2,
                    PackageRegistration = packageRegistration,
                    Version = "1.0.0+metadata",
                };

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0.0", SemVerLevelKey.Unknown, true))
                    .Returns(package);

                var controller = CreateController(GetConfigurationService(), packageService: packageService);
                controller.SetCurrentUser(user);

                // Act
                var result = controller.Delete("Foo", "1.0.0");

                // Assert
                var model = ResultAssert.IsView<DeletePackageViewModel>(result);
                Assert.True(model.IsLocked);
            }

            private ActionResult GetDeleteResult(User currentUser, User owner, out PackagesController controller)
            {
                _packageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersion(_packageId, _package.Version, SemVerLevelKey.Unknown, true))
                    .Returns(_package).Verifiable();

                controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);
                
                var result = controller.Delete(_packageId, _package.Version);
                
                packageService.Verify();

                return result;
            }
        }

        public class TheUpdateListedMethod : TestContainer
        {
            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 5535 }
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task Returns401IfNotOwner(User currentUser, User owner)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package);
                // Note: this Mock must be strict because it guarantees that MarkPackageListedAsync is not called!

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.Edit("Foo", "1.0", listed: false, urlFactory: (pkg, relativeUrl) => @"~\Bar.cshtml");

                // Assert
                Assert.IsType<HttpStatusCodeResult>(result);
                var httpStatusCodeResult = result as HttpStatusCodeResult;
                Assert.Equal(401, httpStatusCodeResult.StatusCode);
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task UpdatesUnlistedIfSelected(User currentUser, User owner)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.MarkPackageListedAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.MarkPackageUnlistedAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult(0)).Verifiable();
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package).Verifiable();

                var indexingService = new Mock<IIndexingService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService);
                controller.SetCurrentUser(currentUser);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.Edit("Foo", "1.0", listed: false, urlFactory: (pkg, relativeUrl) => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                indexingService.Verify(i => i.UpdatePackage(package));
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task UpdatesUnlistedIfNotSelected(User currentUser, User owner)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.MarkPackageListedAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult(0)).Verifiable();
                packageService.Setup(svc => svc.MarkPackageUnlistedAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package).Verifiable();

                var indexingService = new Mock<IIndexingService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService);
                controller.SetCurrentUser(currentUser);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.Edit("Foo", "1.0", listed: true, urlFactory: (pkg, relativeUrl) => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                indexingService.Verify(i => i.UpdatePackage(package));
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
            }

            [Fact]
            public async Task WhenPackageRegistrationIsLockedReturns403()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo", IsLocked = true },
                    Version = "1.0",
                };
                package.PackageRegistration.Owners.Add(new User("Frodo"));

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.MarkPackageListedAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.MarkPackageUnlistedAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package);

                var controller = CreateController(GetConfigurationService(), packageService: packageService);

                controller.SetCurrentUser(new User("Frodo"));
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.Edit("Foo", "1.0", listed: true, urlFactory: (pkg, relativeUrl) => @"~\Bar.cshtml");

                // Assert
                ResultAssert.IsStatusCode(result, HttpStatusCode.Forbidden);
            }
        }

        public class TheEditMethods
            : TestContainer
        {
            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 5535 }
                    };
                }
            }

            protected PackagesController SetupController(
                User currentUser,
                User owner,
                bool hasReadMe = false,
                bool isPackageLocked = false,
                Mock<IPackageFileService> packageFileService = null)
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "packageId", IsLocked = isPackageLocked },
                    Version = "1.0",
                    Listed = true,
                    HasReadMe = hasReadMe,
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>();
                packageService.Setup(s => s.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>()))
                    .Returns(package);
                packageService.Setup(s => s.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(package.PackageRegistration);
                
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    packageFileService: packageFileService);
                controller.SetCurrentUser(currentUser);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                return controller;
            }
        }

        public class TheEditGetMethod
            : TheEditMethods
        {
            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task Returns403IfNotOwner(User currentUser, User owner)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0", null, true))
                    .Returns(package);
                // Note: this Mock must be strict because it guarantees that MarkPackageListedAsync is not called!

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.Edit("Foo", "1.0");

                // Assert
                Assert.IsType<HttpStatusCodeResult>(result);
                var httpStatusCodeResult = result as HttpStatusCodeResult;
                Assert.Equal(403, httpStatusCodeResult.StatusCode);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task UsesNormalizedVersionsInUrlsInSelectList(User currentUser, User owner)
            {
                // Arrange
                var packageRegistration = new PackageRegistration { Id = "Foo" };
                packageRegistration.Owners.Add(owner);

                var package = new Package
                {
                    Key = 2,
                    PackageRegistration = packageRegistration,
                    Version = "1.0.0+metadata",
                    Listed = true,
                    IsLatestSemVer2 = true,
                    HasReadMe = false
                };
                var olderPackageVersion = new Package
                {
                    Key = 1,
                    PackageRegistration = packageRegistration,
                    Version = "1.0.0-alpha",
                    IsLatest = true,
                    IsLatestSemVer2 = true,
                    Listed = true,
                    HasReadMe = false
                };

                packageRegistration.Packages.Add(package);
                packageRegistration.Packages.Add(olderPackageVersion);
                
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0.0", SemVerLevelKey.Unknown, true))
                    .Returns(package).Verifiable();
                

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                // Act
                var result = await controller.Edit("Foo", "1.0.0");

                // Assert
                packageService.Verify();

                Assert.IsType<ViewResult>(result);
                var model = ((ViewResult)result).Model as EditPackageRequest;
                Assert.NotNull(model);

                // Verify version select list
                Assert.Equal(packageRegistration.Packages.Count, model.VersionSelectList.Count());

                foreach (var pkg in packageRegistration.Packages)
                {
                    var valueField = UrlExtensions.EditPackage(controller.Url, model.PackageId, pkg.NormalizedVersion);
                    var textField = NuGetVersion.Parse(pkg.Version).ToFullString() + (pkg.IsLatestSemVer2 ? " (Latest)" : string.Empty);

                    var selectListItem = model.VersionSelectList
                        .SingleOrDefault(i => string.Equals(i.Text, textField) && string.Equals(i.Value, valueField));

                    Assert.NotNull(selectListItem);
                    Assert.Equal(valueField, selectListItem.Value);
                    Assert.Equal(textField, selectListItem.Text);
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task WhenNoReadMeEditPending_ReturnsActive(User currentUser, User owner)
            {
                // Arrange
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();

                var controller = SetupController(currentUser, owner, hasReadMe: true, packageFileService: packageFileService);

                // Act.
                var result = await controller.Edit("packageId", "1.0");

                // Assert.
                var model = ResultAssert.IsView<EditPackageRequest>(result);

                Assert.NotNull(model?.Edit?.ReadMe);
                Assert.Equal("Written", model.Edit.ReadMe.SourceType);
                Assert.Equal("markdown", model.Edit.ReadMe.SourceText);

                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()), Times.Once);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task WhenNoReadMe_ReturnsNull(User currentUser, User owner)
            {
                // Arrange
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();

                var controller = SetupController(currentUser, owner, packageFileService: packageFileService);

                // Act.
                var result = await controller.Edit("packageId", "1.0");

                // Assert.
                var model = ResultAssert.IsView<EditPackageRequest>(result);

                Assert.NotNull(model?.Edit?.ReadMe);
                Assert.Null(model.Edit.ReadMe.SourceType);
                Assert.Null(model.Edit.ReadMe.SourceText);

                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()), Times.Never);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            [MemberData(nameof(NotOwner_Data))]
            public async Task WhenPackageIsNotFoundReturns404(User currentUser, User owner)
            {
                // Arrange
                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>()))
                              .Returns<Package>(null);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);

                // Act
                var result = await controller.Edit("Foo", "1.0.0");

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task WhenPackageRegistrationIsLocked_ReturnsLocked(User currentUser, User owner)
            {
                // Arrange
                var controller = SetupController(currentUser, owner, isPackageLocked: true);

                // Act
                var result = await controller.Edit("packageId", "1.0.0");

                // Assert
                var model = ResultAssert.IsView<EditPackageRequest>(result);
                Assert.True(model.IsLocked);
                Assert.Null(model.PackageVersions);
                Assert.Null(model.VersionSelectList);
                Assert.Null(model.Edit);
            }
        }

        public class TheEditPostMethod : TheEditMethods
        {
            public static IEnumerable<object[]> OnPostBackWithReadMe_Saves_Data
            {
                get
                {
                    foreach (var ownerData in Owner_Data)
                    {
                        foreach (var hasReadMe in new[] { false, true })
                        {
                            yield return ownerData.Concat(new object[] { hasReadMe }).ToArray();
                        }
                    }
                }
            }
            
            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task Returns401IfNotOwner(User currentUser, User owner)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package);
                // Note: this Mock must be strict because it guarantees that MarkPackageListedAsync is not called!

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.Edit("Foo", "1.0", listed: false, urlFactory: (pkg, relativeUrl) => @"~\Bar.cshtml");

                // Assert
                Assert.IsType<HttpStatusCodeResult>(result);
                var httpStatusCodeResult = result as HttpStatusCodeResult;
                Assert.Equal(401, httpStatusCodeResult.StatusCode);
            }

            [Theory]
            [MemberData(nameof(OnPostBackWithReadMe_Saves_Data))]
            public async Task OnPostBackWithReadMe_Saves(User currentUser, User owner, bool hasReadMe)
            {
                // Arrange
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();
                packageFileService.Setup(s => s.SaveReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var controller = SetupController(currentUser, owner, hasReadMe: hasReadMe, packageFileService: packageFileService);

                var formData = new VerifyPackageRequest
                {
                    Edit = new EditPackageVersionReadMeRequest
                    {
                        ReadMe = new ReadMeRequest
                        {
                            SourceType = "written",
                            SourceText = "markdown2"
                        }
                    }
                };

                // Act.
                var result = await controller.Edit("packageId", "1.0", formData, "returnUrl");

                // Assert.
                packageFileService.Verify(s => s.SaveReadMeMdFileAsync(It.IsAny<Package>(), "markdown2"));

                // Verify that a comparison was done against the active readme.
                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()), Times.Exactly(hasReadMe ? 1 : 0));
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task OnPostBackWithNoReadMe_Deletes(User currentUser, User owner)
            {
                // Arrange
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();
                packageFileService.Setup(s => s.DeleteReadMeMdFileAsync(It.IsAny<Package>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var controller = SetupController(currentUser, owner, hasReadMe: true, packageFileService: packageFileService);

                var formData = new VerifyPackageRequest
                {
                    Edit = new EditPackageVersionReadMeRequest()
                };

                // Act.
                var result = await controller.Edit("packageId", "1.0", formData, "returnUrl");

                // Assert.
                packageFileService.Verify(s => s.DeleteReadMeMdFileAsync(It.IsAny<Package>()));

                // Verify that a comparison was done against the active readme.
                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>()), Times.Once);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task WhenPackageRegistrationIsLockedReturns403(User currentUser, User owner)
            {
                // Arrange
                var controller = SetupController(currentUser, owner, isPackageLocked: true);

                // Act
                var result = await controller.Edit("packageId", "1.0.0", new VerifyPackageRequest(), string.Empty);

                // Assert
                Assert.IsType<JsonResult>(result);
                Assert.Equal(403, controller.Response.StatusCode);
            }
        }

        public class TheListPackagesMethod
            : TestContainer
        {
            [Fact]
            public async Task TrimsSearchTerm()
            {
                var searchService = new Mock<ISearchService>();
                searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns(
                    Task.FromResult(new SearchResults(0, DateTime.UtcNow)));
                var controller = CreateController(
                    GetConfigurationService(),
                    searchService: searchService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = (await controller.ListPackages(new PackageListSearchViewModel() { Q = " test " })) as ViewResult;

                var model = result.Model as PackageListViewModel;
                Assert.Equal("test", model.SearchTerm);
            }

            [Fact]
            public async Task DefaultsToFirstPageAndIncludingPrerelease()
            {
                var searchService = new Mock<ISearchService>();
                searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns(
                    Task.FromResult(new SearchResults(0, DateTime.UtcNow)));
                var controller = CreateController(
                    GetConfigurationService(),
                    searchService: searchService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = (await controller.ListPackages(new PackageListSearchViewModel { Q = "test" })) as ViewResult;

                var model = result.Model as PackageListViewModel;
                Assert.True(model.IncludePrerelease);
                Assert.Equal(0, model.PageIndex);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task PassesPrerelParameterOnToSearchFilter(bool prerel)
            {
                var searchService = new Mock<ISearchService>();
                searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns(
                    Task.FromResult(new SearchResults(0, DateTime.UtcNow)));
                var controller = CreateController(
                    GetConfigurationService(),
                    searchService: searchService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = (await controller.ListPackages(new PackageListSearchViewModel { Q = "test", Prerel = prerel })) as ViewResult;

                var model = result.Model as PackageListViewModel;
                Assert.Equal(prerel, model.IncludePrerelease);
                searchService.Verify(x => x.Search(It.Is<SearchFilter>(f => f.IncludePrerelease == prerel)));
            }
        }

        public class TheManagePackageOwnersMethod
            : TestContainer
        {
            private string _packageId = "CrestedGecko";
            private string _packageVersion = "3.4.2";

            private Package _package;

            public TheManagePackageOwnersMethod()
            {
                _package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = _packageId },
                    Version = _packageVersion
                };
            }

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        new User { Key = 1553 }
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public void Returns401IfNotOwner(User currentUser, User owner)
            {
                var result = GetManagePackageOwnersResult(currentUser, owner);

                Assert.IsType<HttpStatusCodeResult>(result);
                var httpStatusCodeResult = result as HttpStatusCodeResult;
                Assert.Equal(401, httpStatusCodeResult.StatusCode);
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser,
                        false
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        TestUtility.FakeUser,
                        true
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization,
                        false
                    };
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public void ShowsPageIfOwner(User currentUser, User owner, bool isSiteAdmin)
            {
                var result = GetManagePackageOwnersResult(currentUser, owner);

                Assert.IsType<ViewResult>(result);
                var viewResult = result as ViewResult;

                Assert.IsType<ManagePackageOwnersViewModel>(viewResult.Model);
                var model = viewResult.Model as ManagePackageOwnersViewModel;
                Assert.Equal(_packageId, model.Id);
                Assert.Equal(_packageVersion, model.Version);
                Assert.Equal(isSiteAdmin, model.IsCurrentUserAnAdmin);
            }

            private ActionResult GetManagePackageOwnersResult(User currentUser, User owner)
            {
                _package.PackageRegistration.Owners = new[] { owner };

                var packageService = new Mock<IPackageService>();
                packageService
                    .Setup(p => p.FindPackageByIdAndVersion(_packageId, string.Empty, null, true))
                    .Returns(_package);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);

                return controller.ManagePackageOwners(_packageId);
            }
        }

        public class TheReportAbuseMethod
            : TestContainer
        {
            public static string PackageId = "gollum";
            public static string PackageVersion = "2.0.1";
            public static string UnencodedMessage = "Gollum took my <b>finger</bold>";
            public static string EncodedMessage = "Gollum took my &lt;b&gt;finger&lt;/bold&gt;";
            public static string ReporterEmailAddress = "frodo@hobbiton.example.com";
            public static string Signature = "Frodo";
            public static User Owner = new User { Key = 313, Username = "Gollum", EmailAddress = "gollum@mordor.com" };

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        Owner
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        Owner
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        Owner
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public void ShowsFormWhenNotOwner(User currentUser, User owner)
            {
                var result = GetReportAbuseResult(currentUser, owner, out var package);

                Assert.IsType<ViewResult>(result);
                var viewResult = result as ViewResult;

                Assert.IsType<ReportAbuseViewModel>(viewResult.Model);
                var model = viewResult.Model as ReportAbuseViewModel;

                Assert.Equal(PackageId, model.PackageId);
                Assert.Equal(PackageVersion, model.PackageVersion);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public void RedirectsToReportMyPackageWhenOwner(User currentUser, User owner)
            {
                var result = GetReportAbuseResult(currentUser, owner, out var package);
                
                Assert.IsType<RedirectToRouteResult>(result);
                var redirectResult = result as RedirectToRouteResult;
                Assert.Equal("ReportMyPackage", redirectResult.RouteValues["Action"]);
            }

            public ActionResult GetReportAbuseResult(User currentUser, User owner, out Package package)
            {
                package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = PackageId, Owners = { owner } },
                    Version = PackageVersion
                };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(PackageId, PackageVersion)).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    httpContext: httpContext);
                controller.SetCurrentUser(currentUser);
                TestUtility.SetupUrlHelper(controller, httpContext);

                return controller.ReportAbuse(PackageId, PackageVersion);
            }

            [Fact]
            public async Task FormSendsMessageToGalleryOwnerWithEmailOnlyWhenUnauthenticated()
            {
                var result = await GetReportAbuseFormResult(null, Owner, out var package, out var messageService);

                Assert.NotNull(result);
                messageService.Verify(
                    s => s.ReportAbuse(
                        It.Is<ReportPackageRequest>(
                            r => r.FromAddress.Address == ReporterEmailAddress
                                 && r.Package == package
                                 && r.Reason == EnumHelper.GetDescription(ReportPackageReason.ViolatesALicenseIOwn)
                                 && r.Message == EncodedMessage
                                 && r.AlreadyContactedOwners)));
            }

            public static IEnumerable<object[]> FormSendsMessageToGalleryOwnerWithUserInfoWhenAuthenticated_Data
            {
                get
                {
                    var authenticatedUserTest = new[] 
                    {
                        new object[]
                        {
                            TestUtility.FakeUser,
                            Owner
                        }
                    };

                    return authenticatedUserTest.Concat(Owner_Data);
                }
            }

            [Theory]
            [MemberData(nameof(FormSendsMessageToGalleryOwnerWithUserInfoWhenAuthenticated_Data))]
            public async Task FormSendsMessageToGalleryOwnerWithUserInfoWhenAuthenticated(User currentUser, User owner)
            {
                var result = await GetReportAbuseFormResult(currentUser, owner, package: out var package, messageService: out var messageService);

                Assert.NotNull(result);
                messageService.Verify(
                    s => s.ReportAbuse(
                        It.Is<ReportPackageRequest>(
                            r => r.Message == EncodedMessage
                                 && r.FromAddress.Address == currentUser.EmailAddress
                                 && r.FromAddress.DisplayName == currentUser.Username
                                 && r.Package == package
                                 && r.Reason == EnumHelper.GetDescription(ReportPackageReason.ViolatesALicenseIOwn)
                                 && r.AlreadyContactedOwners)));
            }

            public Task<ActionResult> GetReportAbuseFormResult(User currentUser, User owner, out Package package, out Mock<IMessageService> messageService)
            {
                messageService = new Mock<IMessageService>();
                messageService.Setup(
                    s => s.ReportAbuse(It.Is<ReportPackageRequest>(r => r.Message == UnencodedMessage)));
                package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = PackageId, Owners = new[] { owner } },
                    Version = "2.0.1"
                };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict(PackageId, PackageVersion)).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    messageService: messageService,
                    httpContext: httpContext);
                controller.SetCurrentUser(currentUser);
                var model = new ReportAbuseViewModel
                {
                    Email = ReporterEmailAddress,
                    Message = UnencodedMessage,
                    Reason = ReportPackageReason.ViolatesALicenseIOwn,
                    AlreadyContactedOwner = true,
                    Signature = Signature
                };

                if (currentUser != null)
                {
                    model.Email = currentUser.EmailAddress;
                }

                TestUtility.SetupUrlHelper(controller, httpContext);
                return controller.ReportAbuse(PackageId, PackageVersion, model);
            }
        }

        public class TheReportMyPackageMethod
            : TestContainer
        {
            private Package _package;
            private static User _owner = new User { EmailAddress = "frodo@hobbiton.example.com", Username = "Frodo", Key = 2 };
            private ReportMyPackageViewModel _viewModel;
            private Issue _supportRequest;
            private Mock<IPackageService> _packageService;
            private Mock<IMessageService> _messageService;
            private Mock<IPackageDeleteService> _packageDeleteService;
            private Mock<ISupportRequestService> _supportRequestService;
            private PackagesController _controller;

            public void SetupTest(User currentUser, User owner)
            {
                if (owner == null)
                {
                    owner = _owner;
                }

                _package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "Mordor",
                        Owners = { owner },
                    },
                    Version = "2.00.1",
                    NormalizedVersion = "2.0.1",
                };

                _viewModel = new ReportMyPackageViewModel
                {
                    Reason = ReportPackageReason.ContainsPrivateAndConfidentialData,
                    Message = "Message!",
                };

                _packageService = new Mock<IPackageService>();
                _packageService
                    .Setup(p => p.FindPackageByIdAndVersionStrict(_package.PackageRegistration.Id, _package.Version))
                    .Returns(_package);

                _messageService = new Mock<IMessageService>();
                _packageDeleteService = new Mock<IPackageDeleteService>();

                _supportRequest = new Issue
                {
                    Key = 23,
                };

                _supportRequestService = new Mock<ISupportRequestService>();
                _supportRequestService
                    .Setup(x => x.AddNewSupportRequestAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<User>(),
                        It.IsAny<Package>()))
                    .ReturnsAsync(() => _supportRequest);

                var httpContext = new Mock<HttpContextBase>();
                _controller = CreateController(
                    GetConfigurationService(),
                    packageService: _packageService,
                    messageService: _messageService,
                    packageDeleteService: _packageDeleteService,
                    supportRequestService: _supportRequestService,
                    httpContext: httpContext);
                _controller.SetCurrentUser(currentUser);

                TestUtility.SetupUrlHelper(_controller, httpContext);
            }

            public static IEnumerable<object[]> OwnerAndNotOwner_Data => Owner_Data.Concat(NotOwner_Data);

            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        null,
                        null
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        null
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeAdminUser,
                        null
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationCollaborator,
                        TestUtility.FakeOrganization
                    };
                }
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    yield return new object[]
                    {
                        TestUtility.FakeUser,
                        TestUtility.FakeUser
                    };

                    yield return new object[]
                    {
                        TestUtility.FakeOrganizationAdmin,
                        TestUtility.FakeOrganization
                    };
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task GetRedirectsNonOwnersToReportAbuse(User currentUser, User owner)
            {
                await RedirectsNonOwnersToReportAbuse(
                    currentUser, owner,
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version));
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task PostRedirectsNonOwnersToReportAbuse(User currentUser, User owner)
            {
                await RedirectsNonOwnersToReportAbuse(
                    currentUser, owner,
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version,
                        _viewModel));
            }

            private async Task RedirectsNonOwnersToReportAbuse(User currentUser, User owner, Func<Task<ActionResult>> actAsync)
            {
                SetupTest(currentUser, owner);
                var result = await actAsync();
                
                Assert.IsType<RedirectToRouteResult>(result);
                Assert.Equal("ReportAbuse", ((RedirectToRouteResult)result).RouteValues["Action"]);
            }

            [Theory]
            [MemberData(nameof(OwnerAndNotOwner_Data))]
            public async Task GetRedirectsMissingPackageToNotFound(User currentUser, User owner)
            {
                await RedirectsMissingPackageToNotFound(
                    currentUser, owner,
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version));
            }

            [Theory]
            [MemberData(nameof(OwnerAndNotOwner_Data))]
            public async Task PostRedirectsMissingPackageToNotFound(User currentUser, User owner)
            {
                await RedirectsMissingPackageToNotFound(
                    currentUser, owner,
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version,
                        _viewModel));
            }

            private async Task RedirectsMissingPackageToNotFound(User currentUser, User owner, Func<Task<ActionResult>> actAsync)
            {
                // Arrange
                SetupTest(currentUser: null, owner: _owner);
                _packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns<Package>(null);

                // Act
                var result = await actAsync();

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task HtmlEncodesMessageContent(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.Message = "I like the cut of your jib. It's <b>bold</b>.";
                _viewModel.Reason = ReportPackageReason.ViolatesALicenseIOwn;

                ReportPackageRequest reportRequest = null;
                _messageService
                    .Setup(s => s.ReportMyPackage(It.IsAny<ReportPackageRequest>()))
                    .Callback<ReportPackageRequest>(r => reportRequest = r);

                // Act
                await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.NotNull(reportRequest);
                Assert.Equal(currentUser.EmailAddress, reportRequest.FromAddress.Address);
                Assert.Same(_package, reportRequest.Package);
                Assert.Equal(EnumHelper.GetDescription(ReportPackageReason.ViolatesALicenseIOwn), reportRequest.Reason);
                Assert.Equal("I like the cut of your jib. It&#39;s &lt;b&gt;bold&lt;/b&gt;.", reportRequest.Message);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task DoesNotCheckDeleteAllowedIfDeleteWasNotRequested(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.DeleteDecision = PackageDeleteDecision.ContactSupport;
                _viewModel.Message = "Test message!";

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                _packageDeleteService.Verify(
                    x => x.CanPackageBeDeletedByUserAsync(
                        It.IsAny<Package>()),
                    Times.Never);
                _supportRequestService.Verify(
                    x => x.AddNewSupportRequestAsync(
                        string.Format(
                            Strings.OwnerSupportRequestSubjectFormat,
                            _package.PackageRegistration.Id,
                            _package.NormalizedVersion),
                        "Test message!",
                        currentUser.EmailAddress,
                        EnumHelper.GetDescription(_viewModel.Reason.Value),
                        currentUser,
                        _package),
                    Times.Once);
                Assert.Equal(Strings.SupportRequestSentTransientMessage, _controller.TempData["Message"]);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task AllowsPackageDelete(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.DeleteDecision = PackageDeleteDecision.DeletePackage;
                _viewModel.DeleteConfirmation = true;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(It.IsAny<Package>()))
                    .ReturnsAsync(true);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.IsType<RedirectResult>(result);

                _supportRequestService.Verify(
                    x => x.AddNewSupportRequestAsync(
                        string.Format(
                            Strings.OwnerSupportRequestSubjectFormat,
                            _package.PackageRegistration.Id,
                            _package.NormalizedVersion),
                        Strings.UserPackageDeleteSupportRequestMessage,
                        currentUser.EmailAddress,
                        EnumHelper.GetDescription(_viewModel.Reason.Value),
                        currentUser,
                        _package),
                    Times.Once);
                _packageDeleteService.Verify(
                    x => x.SoftDeletePackagesAsync(
                        It.Is<IEnumerable<Package>>(p => p.First() == _package),
                        currentUser,
                        EnumHelper.GetDescription(_viewModel.Reason.Value),
                        Strings.UserPackageDeleteSignature),
                    Times.Once);
                _supportRequestService.Verify(
                    x => x.UpdateIssueAsync(
                        _supportRequest.Key,
                        null,
                        IssueStatusKeys.Resolved,
                        null,
                        currentUser.Username),
                    Times.Once);
                _messageService.Verify(
                    x => x.SendPackageDeletedNotice(
                        _package,
                        It.IsAny<string>(),
                        It.IsAny<string>()),
                    Times.Once);
                Assert.Equal(Strings.UserPackageDeleteCompleteTransientMessage, _controller.TempData["Message"]);

                _messageService.Verify(
                    x => x.ReportMyPackage(It.IsAny<ReportPackageRequest>()),
                    Times.Never);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task TreatsDeleteFailureAsNormalRequest(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.DeleteDecision = PackageDeleteDecision.DeletePackage;
                _viewModel.DeleteConfirmation = true;
                _viewModel.CopySender = true;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(It.IsAny<Package>()))
                    .ReturnsAsync(true);
                _packageDeleteService
                    .Setup(x => x.SoftDeletePackagesAsync(
                        It.IsAny<IEnumerable<Package>>(),
                        It.IsAny<User>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                    .Throws(new Exception("bad!"));

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.IsType<RedirectResult>(result);
                Assert.False(_viewModel.CopySender, "The sender is not copied when an automatic delete fails.");
                _packageDeleteService.Verify(
                    x => x.SoftDeletePackagesAsync(
                        It.Is<IEnumerable<Package>>(p => p.First() == _package),
                        currentUser,
                        EnumHelper.GetDescription(_viewModel.Reason.Value),
                        Strings.UserPackageDeleteSignature),
                    Times.Once);
                _supportRequestService.Verify(
                    x => x.UpdateIssueAsync(
                        It.IsAny<int>(),
                        It.IsAny<int?>(),
                        It.IsAny<int>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()),
                    Times.Never);
                _messageService.Verify(
                    x => x.SendPackageDeletedNotice(
                        It.IsAny<Package>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()),
                    Times.Never);
                _messageService.Verify(
                    x => x.ReportMyPackage(It.IsAny<ReportPackageRequest>()),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task RequiresMessageWhenNotDeleting(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.DeleteDecision = PackageDeleteDecision.ContactSupport;
                _viewModel.Message = null;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(It.IsAny<Package>()))
                    .ReturnsAsync(true);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.Contains(
                    Strings.MessageIsRequired,
                    _controller
                        .ModelState
                        .Values
                        .SelectMany(x => x.Errors)
                        .Select(x => x.ErrorMessage));
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task RequiresConfirmationWhenDeleting(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.DeleteDecision = PackageDeleteDecision.DeletePackage;
                _viewModel.DeleteConfirmation = false;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(It.IsAny<Package>()))
                    .ReturnsAsync(true);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.Contains(
                    Strings.UserPackageDeleteConfirmationIsRequired,
                    _controller
                        .ModelState
                        .Values
                        .SelectMany(x => x.Errors)
                        .Select(x => x.ErrorMessage));
            }

            private static IEnumerable<ReportPackageReason> ReasonsRequiringDeleteDecision = new[]
            {
                ReportPackageReason.ContainsMaliciousCode,
                ReportPackageReason.ContainsPrivateAndConfidentialData,
                ReportPackageReason.ReleasedInPublicByAccident
            };

            private static IEnumerable<ReportPackageReason> ReasonsNotRequiringDeleteDecision = new[]
            {
                ReportPackageReason.Other
            };

            private static IEnumerable<object[]> MergeOwnersWithReasons(IEnumerable<ReportPackageReason> reasons)
            {
                foreach (var ownerData in Owner_Data)
                {
                    foreach (var reason in reasons)
                    {
                        yield return ownerData.Concat(new object[] { reason }).ToArray();
                    }
                }
            }

            public static IEnumerable<object[]> RequiresDeleteDecision_Data => MergeOwnersWithReasons(ReasonsRequiringDeleteDecision);

            [Theory]
            [MemberData(nameof(RequiresDeleteDecision_Data))]
            public async Task RequiresDeleteDecision(User currentUser, User owner, ReportPackageReason reason)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.Reason = reason;
                _viewModel.DeleteDecision = null;
                _viewModel.DeleteConfirmation = true;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(It.IsAny<Package>()))
                    .ReturnsAsync(true);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.Contains(
                    Strings.UserPackageDeleteDecisionIsRequired,
                    _controller
                        .ModelState
                        .Values
                        .SelectMany(x => x.Errors)
                        .Select(x => x.ErrorMessage));
            }

            public static IEnumerable<object[]> DoesNotRequireDeleteDecision_Data => MergeOwnersWithReasons(ReasonsNotRequiringDeleteDecision);

            [Theory]
            [MemberData(nameof(DoesNotRequireDeleteDecision_Data))]
            public async Task DoesNotRequireDeleteDecision(User currentUser, User owner, ReportPackageReason reason)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.Reason = reason;
                _viewModel.DeleteDecision = null;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(It.IsAny<Package>()))
                    .ReturnsAsync(false);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(
                    Strings.SupportRequestSentTransientMessage,
                    _controller.TempData["Message"]);

                _packageDeleteService.Verify(
                    x => x.SoftDeletePackagesAsync(
                        It.IsAny<IEnumerable<Package>>(),
                        It.IsAny<User>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()),
                    Times.Never);
                _messageService.Verify(
                    x => x.ReportMyPackage(It.IsAny<ReportPackageRequest>()),
                    Times.Once);
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task IgnoresDeleteRequestWhenNotAllowed(User currentUser, User owner)
            {
                // Arrange
                SetupTest(currentUser, owner);
                _viewModel.Reason = ReportPackageReason.ContainsPrivateAndConfidentialData;
                _viewModel.DeleteDecision = PackageDeleteDecision.DeletePackage;
                _viewModel.DeleteConfirmation = true;
                _packageDeleteService
                    .Setup(x => x.CanPackageBeDeletedByUserAsync(It.IsAny<Package>()))
                    .ReturnsAsync(false);

                // Act
                var result = await _controller.ReportMyPackage(
                    _package.PackageRegistration.Id,
                    _package.Version,
                    _viewModel);

                // Assert
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(
                    Strings.SupportRequestSentTransientMessage,
                    _controller.TempData["Message"]);

                _packageDeleteService.Verify(
                    x => x.SoftDeletePackagesAsync(
                        It.IsAny<IEnumerable<Package>>(),
                        It.IsAny<User>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()),
                    Times.Never);
                _messageService.Verify(
                    x => x.ReportMyPackage(It.IsAny<ReportPackageRequest>()),
                    Times.Once);
            }
        }

        public class TheUploadFileActionForGetRequests
            : TestContainer
        {
            public static IEnumerable<object[]> WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewId_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, new[] { TestUtility.FakeUser });
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, new[] { TestUtility.FakeAdminUser });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, new[] { TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, new[] { TestUtility.FakeOrganizationCollaborator });
                }
            }

            [Theory]
            [MemberData(nameof(WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewId_Data))]
            public async Task WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewId(User currentUser, IEnumerable<User> expectedPossibleOwners)
            {
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakeUploadFileService = new Mock<IUploadFileService>();
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(0));
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(It.IsAny<int>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(currentUser.Username)).Returns(currentUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        uploadFileService:fakeUploadFileService,
                        userService: fakeUserService);
                    controller.SetCurrentUser(currentUser);

                    var result = (await controller.UploadPackage() as ViewResult).Model as SubmitPackageRequest;

                    Assert.NotNull(result);
                    Assert.True(result.IsUploadInProgress);
                    Assert.NotNull(result.InProgressUpload);
                    AssertIdenticalPossibleOwners(result.InProgressUpload.PossibleOwners, expectedPossibleOwners);
                }
            }

            public static IEnumerable<object[]> WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewIdInReservedNamespace_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, new[] { TestUtility.FakeUser });
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser, new[] { TestUtility.FakeUser });

                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, new[] { TestUtility.FakeAdminUser });
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser, new[] { TestUtility.FakeAdminUser });

                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeUser, new[] { TestUtility.FakeOrganizationAdmin });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization, new[] { TestUtility.FakeOrganization });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeUser, new[] { TestUtility.FakeOrganizationCollaborator });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization, new[] { TestUtility.FakeOrganizationCollaborator });
                }
            }

            [Theory]
            [MemberData(nameof(WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewIdInReservedNamespace_Data))]
            public async Task WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewIdInReservedNamespace(User currentUser, User reservedNamespaceOwner, IEnumerable<User> expectedPossibleOwners)
            {
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakeUploadFileService = new Mock<IUploadFileService>();
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(0));
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(It.IsAny<int>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(currentUser.Username)).Returns(currentUser);

                    var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                    fakeReservedNamespaceService
                        .Setup(x => x.GetReservedNamespacesForId(It.IsAny<string>()))
                        .Returns(new[] { new ReservedNamespace { Owners = new[] { reservedNamespaceOwner } } });

                    var controller = CreateController(
                        GetConfigurationService(),
                        uploadFileService: fakeUploadFileService,
                        userService: fakeUserService,
                        reservedNamespaceService: fakeReservedNamespaceService);
                    controller.SetCurrentUser(currentUser);

                    var result = (await controller.UploadPackage() as ViewResult).Model as SubmitPackageRequest;

                    Assert.NotNull(result);
                    Assert.True(result.IsUploadInProgress);
                    Assert.NotNull(result.InProgressUpload);
                    AssertIdenticalPossibleOwners(result.InProgressUpload.PossibleOwners, expectedPossibleOwners);
                }
            }

            public static IEnumerable<object[]> WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewVersion_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, new[] { TestUtility.FakeUser });
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser, new[] { TestUtility.FakeUser });

                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, new[] { TestUtility.FakeAdminUser });
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser, new[] { TestUtility.FakeAdminUser });

                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeUser, new[] { TestUtility.FakeOrganizationAdmin });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization, new[] { TestUtility.FakeOrganization });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeUser, new[] { TestUtility.FakeOrganizationCollaborator });
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization, new[] { TestUtility.FakeOrganization });
                }
            }

            [Theory]
            [MemberData(nameof(WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewVersion_Data))]
            public async Task WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgressForANewVersion(User currentUser, User existingPackageOwner, IEnumerable<User> expectedPossibleOwners)
            {
                var packageId = "CrestedGecko";
                var packageVersion = "1.4.2";

                var fakeFileStream = TestPackage.CreateTestPackageStream(packageId, packageVersion);
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(0));
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(It.IsAny<int>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var fakePackageService = new Mock<IPackageService>();
                fakePackageService
                    .Setup(x => x.FindPackageRegistrationById(packageId))
                    .Returns(new PackageRegistration { Id = packageId, Owners = new[] { existingPackageOwner } });

                var fakeUserService = new Mock<IUserService>();
                fakeUserService.Setup(x => x.FindByUsername(currentUser.Username)).Returns(currentUser);

                var controller = CreateController(
                    GetConfigurationService(),
                    userService: fakeUserService,
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeFileStream,
                    packageService: fakePackageService);
                controller.SetCurrentUser(currentUser);

                var result = (await controller.UploadPackage() as ViewResult).Model as SubmitPackageRequest;

                Assert.NotNull(result);
                Assert.True(result.IsUploadInProgress);
                Assert.NotNull(result.InProgressUpload);
                AssertIdenticalPossibleOwners(result.InProgressUpload.PossibleOwners, expectedPossibleOwners);
            }

            [Fact]
            public async Task WillShowTheViewWhenThereIsNoUploadInProgress()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage() as ViewResult;

                Assert.NotNull(result);
            }

            private void AssertIdenticalPossibleOwners(IEnumerable<string> possibleOwners, IEnumerable<User> expectedPossibleOwners)
            {
                Assert.True(possibleOwners.SequenceEqual(expectedPossibleOwners.Select(u => u.Username)));
            }
        }

        public class TheUploadFileActionForPostRequests
            : TestContainer
        {
            [Fact]
            public async Task WillReturn409WhenThereIsAlreadyAnUploadInProgress()
            {
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakeUploadFileService = new Mock<IUploadFileService>();
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    var controller = CreateController(
                        GetConfigurationService(),
                        uploadFileService: fakeUploadFileService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.UploadPackage(null) as JsonResult;

                    Assert.NotNull(result);
                }
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfPackageFileIsNull()
            {
                var controller = CreateController(GetConfigurationService());
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(null) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UploadFileIsRequired, (result.Data as string[])[0]);
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfFileIsNotANuGetPackage()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.notNuPkg");
                var controller = CreateController(GetConfigurationService());
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UploadFileMustBeNuGetPackage, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfEnsureValidThrowsException()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var readPackageException = new Exception();

                var controller = CreateController(
                    GetConfigurationService(),
                    readPackageException: readPackageException);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.FailedToReadUploadFile, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            private const string EnsureValidExceptionMessage = "naughty package";

            [Theory]
            [InlineData(typeof(InvalidPackageException), true)]
            [InlineData(typeof(InvalidDataException), true)]
            [InlineData(typeof(EntityException), true)]
            [InlineData(typeof(Exception), false)]
            public async Task WillShowViewWithErrorsIfEnsureValidThrowsExceptionMessage(Type exceptionType, bool expectExceptionMessageInResponse)
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var readPackageException =
                    exceptionType.GetConstructor(new[] { typeof(string) }).Invoke(new[] { EnsureValidExceptionMessage });

                var controller = CreateController(
                    GetConfigurationService(),
                    readPackageException: readPackageException as Exception);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(expectExceptionMessageInResponse ? EnsureValidExceptionMessage : Strings.FailedToReadUploadFile, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
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
            public async Task WillShowViewWithErrorsIfPackageIdIsInvalid(string packageId)
            {
                // Arrange
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns(packageId + ".nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream(packageId, "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var controller = CreateController(
                    GetConfigurationService(),
                    fakeNuGetPackage: TestPackage.CreateTestPackageStream(packageId, "1.0.0"));
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
            }

            public static IEnumerable<object[]> WillShowTheViewWithErrorsWhenThePackageIdIsAlreadyBeingUsed_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser);
                }
            }

            [Theory]
            [MemberData(nameof(WillShowTheViewWithErrorsWhenThePackageIdIsAlreadyBeingUsed_Data))]
            public async Task WillShowTheViewWithErrorsWhenThePackageIdIsAlreadyBeingUsed(User currentUser, User existingPackageOwner)
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakePackageRegistration = new PackageRegistration { Id = "theId", Owners = new[] { existingPackageOwner } };
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(fakePackageRegistration);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: fakePackageService);
                controller.SetCurrentUser(currentUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(String.Format(Strings.PackageIdNotAvailable, "theId"), controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            public static IEnumerable<object[]> WillShowTheViewWithErrorsWhenThePackageIdMatchesUnownedNamespace_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(WillShowTheViewWithErrorsWhenThePackageIdMatchesUnownedNamespace_Data))]
            public async Task WillShowTheViewWithErrorsWhenThePackageIdMatchesUnownedNamespace(User currentUser, User reservedNamespaceOwner)
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("Random.Package1", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(currentUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));

                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(() => null);

                var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                fakeReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(new[] { new ReservedNamespace { Owners = new[] { new User { Key = 123123123 } } } });

                var fakeTelemetryService = new Mock<ITelemetryService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageService: fakePackageService,
                    fakeNuGetPackage: fakeFileStream,
                    reservedNamespaceService: fakeReservedNamespaceService,
                    telemetryService: fakeTelemetryService);
                controller.SetCurrentUser(currentUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(String.Format(Strings.UploadPackage_IdNamespaceConflict), controller.ModelState[String.Empty].Errors[0].ErrorMessage);
                fakeTelemetryService.Verify(x => x.TrackPackagePushNamespaceConflictEvent(It.IsAny<string>(), It.IsAny<string>(), currentUser, controller.OwinContext.Request.User.Identity), Times.Once);
            }

            public static IEnumerable<object[]> WillUploadThePackageWhenIdMatchesOwnedNamespace_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(WillUploadThePackageWhenIdMatchesOwnedNamespace_Data))]
            public async Task WillUploadThePackageWhenIdMatchesOwnedNamespace(User currentUser, User reservedNamespaceOwner)
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("Random.Package1", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(currentUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(currentUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(() => null);

                var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                fakeReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(new[] { new ReservedNamespace { Owners = new[] { reservedNamespaceOwner } } });

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageService: fakePackageService,
                    fakeNuGetPackage: fakeFileStream,
                    reservedNamespaceService: fakeReservedNamespaceService);
                controller.SetCurrentUser(currentUser);

                await controller.UploadPackage(fakeUploadedFile.Object);

                Assert.True(controller.ModelState.IsValid);
                fakeUploadFileService.Verify(x => x.SaveUploadFileAsync(currentUser.Key, fakeFileStream));
                fakeFileStream.Dispose();
            }

            public static IEnumerable<object[]> WillShowTheViewWithErrorsWhenThePackageAlreadyExists_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(WillShowTheViewWithErrorsWhenThePackageAlreadyExists_Data))]
            public async Task WillUploadThePackageWhenIdMatchesUnownedNamespaceButPackageExists(User currentUser, User existingPackageOwner)
            {
                var packageId = "Random.Package1";

                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream(packageId, "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(currentUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(currentUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(() => new PackageRegistration { Id = packageId, Owners = new[] { existingPackageOwner } });

                var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                fakeReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(new[] { new ReservedNamespace { Owners = new[] { new User { Key = 332331 } } } });

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageService: fakePackageService,
                    fakeNuGetPackage: fakeFileStream,
                    reservedNamespaceService: fakeReservedNamespaceService);
                controller.SetCurrentUser(currentUser);

                await controller.UploadPackage(fakeUploadedFile.Object);

                Assert.True(controller.ModelState.IsValid);
                fakeUploadFileService.Verify(x => x.SaveUploadFileAsync(currentUser.Key, fakeFileStream));
                fakeFileStream.Dispose();
            }

            [Theory]
            [MemberData(nameof(WillShowTheViewWithErrorsWhenThePackageAlreadyExists_Data))]
            public async Task WillShowTheViewWithErrorsWhenThePackageAlreadyExists(User currentUser, User existingPackageOwner)
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = "theId", Owners = new[] { existingPackageOwner } }, Version = "1.0.0" });
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: fakePackageService);
                controller.SetCurrentUser(currentUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(
                    String.Format(Strings.PackageExistsAndCannotBeModified, "theId", "1.0.0"),
                    controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillShowTheViewWithErrorsWhenThePackageAlreadyExistsAndOnlyDiffersByMetadata()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0+metadata2");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "1.0.0+metadata" });
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: fakePackageService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(
                    String.Format(Strings.PackageVersionDiffersOnlyByMetadataAndCannotBeModified, "theId", "1.0.0+metadata"),
                    controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillSaveTheUploadFile()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("thePackageId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeFileStream);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.UploadPackage(fakeUploadedFile.Object);

                fakeUploadFileService.Verify(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, fakeFileStream));
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillRedirectToVerifyPackageActionAfterSaving()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("thePackageId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult(fakeFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));

                var fakeUserService = new Mock<IUserService>();
                fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    userService: fakeUserService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.True(result.Data is VerifyPackageRequest);
            }

            [Fact]
            public async Task WillShowViewWithErrorWhenThePackageRegistrationIsLocked()
            {
                // Arrange
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakePackageRegistration = new PackageRegistration { Id = "theId", IsLocked = true, Owners = new[] { new User { Key = TestUtility.FakeUser.Key } } };
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(fakePackageRegistration);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: fakePackageService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                // Act
                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                // Assert
                Assert.IsType<JsonResult>(result);
                Assert.Equal(403, controller.Response.StatusCode);
            }
        }

        public class TheVerifyPackageActionForPostRequests
            : TestContainer
        {
            [Fact]
            public async Task WillRedirectToUploadPageWhenThereIsNoUploadInProgress()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService);
                TestUtility.SetupUrlHelperForUrlGeneration(controller);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null }) as JsonResult;

                Assert.NotNull(result);
            }

            [Fact]
            public async Task WillReturnConflictIfCommittingReturnsConflict()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    fakePackageUploadService
                        .Setup(x => x.CommitPackageAsync(
                            It.IsAny<Package>(),
                            It.IsAny<Stream>()))
                        .ReturnsAsync(PackageCommitResult.Conflict);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    var jsonResult = Assert.IsType<JsonResult>(result);
                    Assert.Equal((int)HttpStatusCode.Conflict, controller.Response.StatusCode);
                }
            }

            [Fact]
            public async Task WillThrowIfOwnerNonExistent()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                }
            }

            [Fact]
            public async Task WillThrowIfOwnerNotValid()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    var owner = new User { Key = 999, Username = "invalidOwner" };
                    fakeUserService.Setup(x => x.FindByUsername(owner.Username)).Returns(owner);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    Assert.Equal((int)HttpStatusCode.BadRequest, controller.Response.StatusCode);
                }
            }

            public static IEnumerable<object[]> CommitResults => Enum
                .GetValues(typeof(PackageCommitResult))
                .Cast<PackageCommitResult>()
                .Select(r => new object[] { r });

            [Theory]
            [MemberData(nameof(CommitResults))]
            public async Task DoesNotThrowForAnyPackageCommitResult(PackageCommitResult commitResult)
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    fakePackageUploadService
                        .Setup(x => x.CommitPackageAsync(
                            It.IsAny<Package>(),
                            It.IsAny<Stream>()))
                        .ReturnsAsync(commitResult);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    fakePackageUploadService.Verify(x => x.CommitPackageAsync(
                        It.IsAny<Package>(),
                        It.IsAny<Stream>()), Times.Once);
                }
            }

            private static readonly string VerifyCreateTestsPackageId = "thePackageId";
            private static readonly string VerifyCreateTestsPackageVersion = "1.4.2";

            public static IEnumerable<object[]> WillCreateThePackageForNewId_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(WillCreateThePackageForNewId_Data))]

            public Task WillCreateThePackageForNewId(User currentUser, User ownerInForm)
            {
                return VerifyCreateThePackage(currentUser, ownerInForm, succeeds: true);
            }

            [Theory]
            [MemberData(nameof(WillCreateThePackageForNewId_Data))]

            public Task WillCreateThePackageIfOwnerInFormOwnsTheReservedNamespace(User currentUser, User ownerInForm)
            {
                return VerifyCreateThePackage(
                    currentUser, 
                    ownerInForm, 
                    succeeds: true, 
                    reservedNamespaceOwner: ownerInForm);
            }

            public static IEnumerable<object[]> WillCreateThePackageIfOwnerInFormOwnsTheExistingPackage_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(WillCreateThePackageIfOwnerInFormOwnsTheExistingPackage_Data))]
            public Task WillCreateThePackageIfOwnerInFormOwnsTheExistingPackage(User currentUser, User ownerInForm)
            {
                return VerifyCreateThePackage(
                    currentUser, 
                    ownerInForm,
                    succeeds: true, 
                    existingPackageOwner: ownerInForm);
            }

            [Theory]
            [MemberData(nameof(WillCreateThePackageIfOwnerInFormOwnsTheExistingPackage_Data))]
            public Task WillCreateThePackageIfOwnerInFormOwnsTheExistingPackageInReservedNamespace(User currentUser, User ownerInForm)
            {
                return VerifyCreateThePackage(
                    currentUser, 
                    ownerInForm, 
                    succeeds: true, 
                    existingPackageOwner: ownerInForm,
                    reservedNamespaceOwner: new User { Key = 787 });
            }

            public static IEnumerable<object[]> WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheExistingPackage_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization, TestUtility.FakeUser);
                }
            }

            [Theory]
            [MemberData(nameof(WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheExistingPackage_Data))]
            public Task WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheExistingPackage(User currentUser, User ownerInForm, User existingPackageOwner)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.VerifyPackage_OwnerInvalid, ownerInForm.Username, VerifyCreateTestsPackageId);

                return VerifyCreateThePackage(
                    currentUser, 
                    ownerInForm, 
                    succeeds: false, 
                    existingPackageOwner: existingPackageOwner, 
                    expectedMessage: message);
            }

            public static IEnumerable<object[]> WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheReservedNamespace_Data
            {
                get
                {
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeUser, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeAdminUser, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeOrganization, TestUtility.FakeUser);
                }
            }

            [Theory]
            [MemberData(nameof(WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheReservedNamespace_Data))]
            public Task WillNotCreateThePackageIfOwnerInFormDoesNotOwnTheReservedNamespace(User currentUser, User ownerInForm, User reservedNamespaceOwner)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.UploadPackage_IdNamespaceConflict, currentUser.Username, ownerInForm.Username);

                return VerifyCreateThePackage(
                    currentUser, 
                    ownerInForm, 
                    succeeds: false,
                    reservedNamespaceOwner: reservedNamespaceOwner,
                    errorResponseCode: HttpStatusCode.Conflict,
                    expectedMessage: message);
            }

            public static IEnumerable<object[]> WillNotCreateThePackageIfCurrentUserDoesNotHavePermissionsToUploadOnBehalfOfTheOwnerInTheForm_Data
            {
                get
                {
                    // User on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, null, null);
                    // User on behalf of an unrelated user who owns the package/reserved namespace.
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeOrganization, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, null, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeOrganization, TestUtility.FakeOrganization);
                    // User who owns the package/reserved namespace on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeUser, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, null, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeUser, TestUtility.FakeUser);

                    // Admin on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, null, null);
                    // Admin on behalf of an unrelated user who owns the package/reserved namespace.
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, TestUtility.FakeUser, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, null, TestUtility.FakeUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, TestUtility.FakeUser, TestUtility.FakeUser);
                    // Admin who owns the package/reserved namespace on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, TestUtility.FakeAdminUser, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, null, TestUtility.FakeAdminUser);
                    yield return MemberDataHelper.AsData(TestUtility.FakeAdminUser, TestUtility.FakeUser, TestUtility.FakeAdminUser, TestUtility.FakeAdminUser);

                    // Organization admin whose organization owns the package/reserved namespace on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeUser, TestUtility.FakeOrganization, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeUser, null, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationAdmin, TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeOrganization);
                    
                    // Organization collaborator on behalf of its organization.
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization, null, null);
                    // Organization collaborator who owns the package/reserved namespace on behalf of its organization.
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization, null, TestUtility.FakeOrganizationCollaborator);
                    // Organization collaborator whose organization owns the reserved namespace on behalf of its organization.
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeOrganization, null, TestUtility.FakeOrganization);
                    // Organization collaborator whose organization owns the package/reserved namespace on behalf of an unrelated user.
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeUser, TestUtility.FakeOrganization, null);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeUser, null, TestUtility.FakeOrganization);
                    yield return MemberDataHelper.AsData(TestUtility.FakeOrganizationCollaborator, TestUtility.FakeUser, TestUtility.FakeOrganization, TestUtility.FakeOrganization);
                }
            }

            [Theory]
            [MemberData(nameof(WillNotCreateThePackageIfCurrentUserDoesNotHavePermissionsToUploadOnBehalfOfTheOwnerInTheForm_Data))]
            public Task WillNotCreateThePackageIfCurrentUserDoesNotHavePermissionsToUploadOnBehalfOfTheOwnerInTheForm(User currentUser, User ownerInForm, User existingPackageOwner = null, User reservedNamespaceOwner = null)
            {
                var templateString = existingPackageOwner == null ? Strings.UploadPackage_NewIdOnBehalfOfUserNotAllowed : Strings.UploadPackage_NewVersionOnBehalfOfUserNotAllowed;
                var message = string.Format(CultureInfo.CurrentCulture, templateString, currentUser.Username, ownerInForm.Username);

                return VerifyCreateThePackage(
                    currentUser, 
                    ownerInForm, 
                    succeeds: false, 
                    existingPackageOwner: existingPackageOwner, 
                    reservedNamespaceOwner: reservedNamespaceOwner, 
                    expectedMessage: message);
            }

            private async Task VerifyCreateThePackage(User currentUser, User ownerInForm, bool succeeds, User existingPackageOwner = null, User reservedNamespaceOwner = null, HttpStatusCode errorResponseCode = HttpStatusCode.BadRequest, string expectedMessage = null)
            {
                var packageId = VerifyCreateTestsPackageId;
                var packageVersion = VerifyCreateTestsPackageVersion;

                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakePackageService = new Mock<IPackageService>();
                    var existingPackageRegistration = existingPackageOwner == null ? 
                        null : 
                        new PackageRegistration { Id = packageId, Owners = new[] { existingPackageOwner } };
                    fakePackageService
                        .Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                        .Returns(existingPackageRegistration);

                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(currentUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(currentUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = packageId }, Version = packageVersion }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(packageId, packageVersion);

                    var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                    var matchingReservedNamespaces = reservedNamespaceOwner == null ? 
                        Enumerable.Empty<ReservedNamespace>() : 
                        new[] { new ReservedNamespace { Owners = new[] { reservedNamespaceOwner } } };
                    fakeReservedNamespaceService.Setup(x => x.GetReservedNamespacesForId(packageId)).Returns(matchingReservedNamespaces.ToList().AsReadOnly());

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(ownerInForm.Username)).Returns(ownerInForm);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService,
                        packageService: fakePackageService,
                        reservedNamespaceService: fakeReservedNamespaceService);
                    controller.SetCurrentUser(currentUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = ownerInForm.Username });

                    if (succeeds)
                    {
                        fakePackageUploadService.Verify(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()), Times.Once);
                    }
                    else
                    {
                        Assert.Equal((int)errorResponseCode, controller.Response.StatusCode);
                        var jsonResult = Assert.IsType<JsonResult>(result);
                        if (expectedMessage != null)
                        {
                            var message = (jsonResult.Data as string[])[0];
                            Assert.Equal(expectedMessage, message);
                        }
                    }
                }
            }

            [Fact]
            public async Task WillUpdateIndexingService()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                    var fakePackageFileService = new Mock<IPackageFileService>();
                    fakePackageFileService.Setup(x => x.SavePackageFileAsync(fakePackage, It.IsAny<Stream>())).Returns(Task.FromResult(0)).Verifiable();

                    var fakeIndexingService = new Mock<IIndexingService>(MockBehavior.Strict);
                    fakeIndexingService.Setup(f => f.UpdateIndex()).Verifiable();

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        packageFileService: fakePackageFileService,
                        indexingService: fakeIndexingService,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    // Assert
                    fakeIndexingService.Verify();
                }
            }

            [Fact]
            public async Task WillNotCommitChangesToPackageService()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.CompletedTask);
                    var fakePackageService = new Mock<IPackageService>(MockBehavior.Strict);
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    fakePackageService.Setup(x => x.PublishPackageAsync(fakePackage, false))
                        .Returns(Task.CompletedTask);
                    fakePackageService.Setup(x => x.MarkPackageUnlistedAsync(fakePackage, false))
                        .Returns(Task.CompletedTask);
                    fakePackageService.Setup(x => x.FindPackageRegistrationById(fakePackage.PackageRegistration.Id))
                        .Returns<PackageRegistration>(null);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Owner = TestUtility.FakeUser.Username });

                    // There's no assert. If the method completes, it means the test passed because we set MockBehavior to Strict
                    // for the fakePackageService. We verified that it only calls methods passing commitSettings = false.
                }
            }

            [Fact]
            public async Task WillPublishThePackage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Owner = TestUtility.FakeUser.Username });

                    fakePackageService.Verify(x => x.PublishPackageAsync(fakePackage, false), Times.Once());
                }
            }

            [Fact]
            public async Task WillMarkThePackageUnlistedWhenListedArgumentIsFalse()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId", Owners = new[] { TestUtility.FakeUser } }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Owner = TestUtility.FakeUser.Username });

                    fakePackageService.Verify(
                        x => x.MarkPackageUnlistedAsync(It.Is<Package>(p => p.PackageRegistration.Id == "theId" && p.Version == "theVersion"), It.IsAny<bool>()));
                }
            }

            [Theory]
            [InlineData(null)]
            [InlineData(true)]
            public async Task WillNotMarkThePackageUnlistedWhenListedArgumentIsNullorTrue(bool? listed)
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = listed.GetValueOrDefault(true), Owner = TestUtility.FakeUser.Username });

                    fakePackageService.Verify(x => x.MarkPackageUnlistedAsync(It.IsAny<Package>(), It.IsAny<bool>()), Times.Never());
                }
            }

            [Fact]
            public async Task WillDeleteTheUploadFile()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0)).Verifiable();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    var facePackageUploadService = new Mock<IPackageUploadService>();
                    facePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId", Owners = new[] { TestUtility.FakeUser } }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: facePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Owner = TestUtility.FakeUser.Username });

                    fakeUploadFileService.Verify();
                }
            }

            [Fact]
            public async Task WillSetAFlashMessage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Owner = TestUtility.FakeUser.Username });

                    Assert.Equal(String.Format(Strings.SuccessfullyUploadedPackage, "theId", "theVersion"), controller.TempData["Message"]);
                }
            }

            [Fact]
            public async Task WillRedirectToPackagePage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(
                        new VerifyPackageRequest() { Listed = false, Owner = TestUtility.FakeUser.Username }) as JsonResult;

                    Assert.NotNull(result);
                    Assert.NotNull(result.Data);
                    Assert.Equal(
                        "{ location = /?id=theId }",
                        result.Data.ToString());
                }
            }

            [Fact]
            public async Task WillCurateThePackage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId", Owners = new[] { TestUtility.FakeUser } }, Version = "theVersion" };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var fakeAutoCuratePackageCmd = new Mock<IAutomaticallyCuratePackageCommand>();
                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        autoCuratePackageCmd: fakeAutoCuratePackageCmd,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Owner = TestUtility.FakeUser.Username });

                    fakeAutoCuratePackageCmd.Verify(fake => fake.ExecuteAsync(fakePackage, It.IsAny<PackageArchiveReader>(), false));
                }
            }

            [Fact]
            public async Task WritesAnAuditRecord()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId", Owners = new[] { TestUtility.FakeUser } }, Version = "theVersion" };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var auditingService = new TestAuditingService();

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        auditingService: auditingService,
                        userService: fakeUserService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Owner = TestUtility.FakeUser.Username });

                    // Assert
                    Assert.True(auditingService.WroteRecord<PackageAuditRecord>(ar =>
                        ar.Action == AuditedPackageAction.Create
                        && ar.Id == fakePackage.PackageRegistration.Id
                        && ar.Version == fakePackage.Version));
                }
            }

            [Fact]
            public async Task WillSendPackagePublishedEvent()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.CompletedTask);
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                    var fakeTelemetryService = new Mock<ITelemetryService>();

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        telemetryService: fakeTelemetryService,
                        userService: fakeUserService);

                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Owner = TestUtility.FakeUser.Username });

                    // Assert
                    fakeTelemetryService.Verify(x => x.TrackPackagePushEvent(It.IsAny<Package>(), TestUtility.FakeUser, controller.OwinContext.Request.User.Identity), Times.Once);
                }
            }

            [Theory]
            [InlineData(false, false,  true)]
            [InlineData( true, false,  true)]
            [InlineData(false,  true,  true)]
            [InlineData( true,  true, false)]
            public async Task WillSendPackageAddedNotice(bool asyncValidationEnabled, bool blockingValidationEnabled, bool callExpected)
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.CompletedTask);
                    var fakePackageUploadService = new Mock<IPackageUploadService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                    var fakeTelemetryService = new Mock<ITelemetryService>();

                    var configurationService = GetConfigurationService();
                    configurationService.Current.AsynchronousPackageValidationEnabled = asyncValidationEnabled;
                    configurationService.Current.BlockingAsynchronousPackageValidationEnabled = blockingValidationEnabled;

                    var fakeMessageService = new Mock<IMessageService>();

                    var fakeUserService = new Mock<IUserService>();
                    fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                    var controller = CreateController(
                        configurationService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        telemetryService: fakeTelemetryService,
                        messageService: fakeMessageService,
                        userService: fakeUserService);

                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Owner = TestUtility.FakeUser.Username, Edit = null });

                    // Assert
                    fakeMessageService
                        .Verify(ms => ms.SendPackageAddedNotice(fakePackage, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                        Times.Exactly(callExpected ? 1 : 0));
                }
            }
        }

        public static IEnumerable<object[]> WillApplyReadMe_Data
        {
            get
            {
                yield return new object[] { new EditPackageVersionReadMeRequest() {
                    ReadMe = new ReadMeRequest { SourceType = "Written", SourceText = "markdown"} }
                };
            }
        }

        [Theory]
        [MemberData(nameof(WillApplyReadMe_Data))]
        public async Task WillApplyReadMeForWrittenReadMeData(EditPackageVersionReadMeRequest edit)
        {
            // Arrange
            using (var fakeFileStream = new MemoryStream())
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.CompletedTask);

                var packageUploadService = new Mock<IPackageUploadService>();

                var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "thePackageId" }, Version = "1.0.0" };
                packageUploadService.Setup(
                    x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>(),
                        It.IsAny<User>()))
                    .Returns(Task.FromResult(fakePackage));
                
                var fakePackageFileService = new Mock<IPackageFileService>();
                fakePackageFileService.Setup(x => x.SaveReadMeMdFileAsync(fakePackage, It.IsAny<string>())).Returns(Task.CompletedTask);

                var fakeUserService = new Mock<IUserService>();
                fakeUserService.Setup(x => x.FindByUsername(TestUtility.FakeUser.Username)).Returns(TestUtility.FakeUser);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageUploadService: packageUploadService,
                    uploadFileService: fakeUploadFileService,
                    packageFileService: fakePackageFileService,
                    userService: fakeUserService);

                controller.SetCurrentUser(TestUtility.FakeUser);

                // Act
                await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Owner = TestUtility.FakeUser.Username, Edit = edit });

                var hasReadMe = !string.IsNullOrEmpty(edit.ReadMe?.SourceType);
                fakePackageFileService.Verify(x => x.SaveReadMeMdFileAsync(fakePackage, "markdown"), Times.Exactly(hasReadMe ? 1 : 0));
            }
        }

        public class TheUploadProgressAction : TestContainer
        {
            private static readonly string FakeUploadName = "upload-" + TestUtility.FakeUserName;

            [Fact]
            public void WillReturnHttpNotFoundForUnknownUser()
            {
                // Arrange
                var cacheService = new Mock<ICacheService>(MockBehavior.Strict);
                cacheService.Setup(c => c.GetItem(FakeUploadName)).Returns(null);

                var controller = CreateController(
                        GetConfigurationService(),
                        cacheService: cacheService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                // Act
                var result = controller.UploadPackageProgress();

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                Assert.Equal(JsonRequestBehavior.AllowGet, jsonResult.JsonRequestBehavior);
            }

            [Fact]
            public void WillReturnCorrectResultForKnownUser()
            {
                var cacheService = new Mock<ICacheService>(MockBehavior.Strict);
                cacheService.Setup(c => c.GetItem(FakeUploadName))
                            .Returns(new AsyncFileUploadProgress(100) { FileName = "haha", TotalBytesRead = 80 });

                var controller = CreateController(
                        GetConfigurationService(),
                        cacheService: cacheService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                // Act
                var result = controller.UploadPackageProgress() as JsonResult;

                // Assert
                Assert.NotNull(result);
                Assert.Equal(JsonRequestBehavior.AllowGet, result.JsonRequestBehavior);
                Assert.True(result.Data is AsyncFileUploadProgress);
                var progress = (AsyncFileUploadProgress)result.Data;
                Assert.Equal(80, progress.TotalBytesRead);
                Assert.Equal(100, progress.ContentLength);
                Assert.Equal("haha", progress.FileName);
            }
        }

        public class TheSetLicenseReportVisibilityMethod
            : TestContainer
        {
            public static IEnumerable<object[]> NotOwner_Data
            {
                get
                {
                    foreach (var visible in new[] { true, false })
                    {
                        yield return new object[]
                        {
                            null,
                            TestUtility.FakeUser,
                            visible
                        };

                        yield return new object[]
                        {
                            TestUtility.FakeUser,
                            new User { Key = 5535 },
                            visible
                        };
                    }
                }
            }

            [Theory]
            [MemberData(nameof(NotOwner_Data))]
            public async Task Returns401IfNotOwner(User currentUser, User owner, bool visible)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package);
                // Note: this Mock must be strict because it guarantees that SetLicenseReportVisibilityAsync is not called!

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(currentUser);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.SetLicenseReportVisibility("Foo", "1.0", visible: visible, urlFactory: (pkg, relativeUrl) => @"~\Bar.cshtml");

                // Assert
                Assert.IsType<HttpStatusCodeResult>(result);
                var httpStatusCodeResult = result as HttpStatusCodeResult;
                Assert.Equal(401, httpStatusCodeResult.StatusCode);
            }

            public static IEnumerable<object[]> Owner_Data
            {
                get
                {
                    foreach (var visible in new[] { true, false })
                    {
                        yield return new object[]
                        {
                            TestUtility.FakeUser,
                            TestUtility.FakeUser,
                            visible
                        };

                        yield return new object[]
                        {
                            TestUtility.FakeAdminUser,
                            TestUtility.FakeUser,
                            visible
                        };

                        yield return new object[]
                        {
                            TestUtility.FakeOrganizationAdmin,
                            TestUtility.FakeOrganization,
                            visible
                        };

                        yield return new object[]
                        {
                            TestUtility.FakeOrganizationCollaborator,
                            TestUtility.FakeOrganization,
                            visible
                        };
                    }
                }
            }

            [Theory]
            [MemberData(nameof(Owner_Data))]
            public async Task IndexingAndPackageServicesAreUpdated(User currentUser, User owner, bool visible)
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    HideLicenseReport = true
                };
                package.PackageRegistration.Owners.Add(owner);

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.SetLicenseReportVisibilityAsync(It.IsAny<Package>(), It.Is<bool>(t => t == !visible), It.IsAny<bool>()))
                    .Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.SetLicenseReportVisibilityAsync(It.IsAny<Package>(), It.Is<bool>(t => t == visible), It.IsAny<bool>()))
                    .Returns(Task.CompletedTask).Verifiable();
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package).Verifiable();
                
                var indexingService = new Mock<IIndexingService>();

                var controller = CreateController(
                        GetConfigurationService(),
                        packageService: packageService,
                        indexingService: indexingService);
                controller.SetCurrentUser(currentUser);
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.SetLicenseReportVisibility("Foo", "1.0", visible: visible, urlFactory: (pkg, relativeUrl) => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                indexingService.Verify(i => i.UpdatePackage(package));
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
            }
        }

        public class TheRevalidateMethod : TestContainer
        {
            private Package _package;
            private readonly Mock<IPackageService> _packageService;
            private readonly Mock<IValidationService> _validationService;
            private readonly TestGalleryConfigurationService _configurationService;
            private readonly PackagesController _target;

            public TheRevalidateMethod()
            {
                _package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "NuGet.Versioning" },
                    Version = "3.4.0",
                };

                _packageService = new Mock<IPackageService>();
                _packageService
                    .Setup(svc => svc.FindPackageByIdAndVersion(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int?>(),
                        It.IsAny<bool>()))
                    .Returns(_package);

                _validationService = new Mock<IValidationService>();

                _configurationService = GetConfigurationService();

                _target = CreateController(
                    _configurationService,
                    packageService: _packageService,
                    validationService: _validationService);
            }

            [Fact]
            public async Task RevalidateIsCalledWithTheExistingPackage()
            {
                // Arrange & Act
                await _target.Revalidate(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                _validationService.Verify(
                    x => x.RevalidateAsync(_package),
                    Times.Once);
            }

            [Fact]
            public async Task RedirectsAfterRevalidatingPackage()
            {
                // Arrange & Act
                var result = await _target.Revalidate(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                var redirect = Assert.IsType<SafeRedirectResult>(result);
                Assert.Equal($"/?id={_package.PackageRegistration.Id}&version={_package.Version}", redirect.Url);
                Assert.Equal("/", redirect.SafeUrl);
            }

            [Fact]
            public async Task ReturnsNotFoundForUnknownPackage()
            {
                // Arrange
                _packageService
                    .Setup(svc => svc.FindPackageByIdAndVersion(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<int?>(),
                        It.IsAny<bool>()))
                    .Returns<Package>(null);

                // Act
                var result = await _target.Revalidate(
                    _package.PackageRegistration.Id,
                    _package.Version);

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }
        }
    }
}
