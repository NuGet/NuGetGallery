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
            Mock<EditPackageService> editPackageService = null,
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
                uploadFileService.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(null));
                uploadFileService.Setup(x => x.SaveUploadFileAsync(42, It.IsAny<Stream>())).Returns(Task.FromResult(0));
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

            editPackageService = editPackageService ?? new Mock<EditPackageService>();

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
                editPackageService.Object,
                packageDeleteService.Object,
                supportRequestService.Object,
                auditingService,
                telemetryService.Object,
                securityPolicyService.Object,
                reservedNamespaceService.Object,
                packageUploadService.Object,
                new ReadMeService(packageFileService.Object),
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
            public async Task GivenANonExistantPackageIt404s()
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

            [Fact]
            public async Task GivenAValidPackageThatTheCurrentUserDoesNotOwnItDisplaysCurrentMetadata()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    indexingService: indexingService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                packageService.Setup(p => p.FindPackageByIdAndVersion("Foo", "1.1.1", SemVerLevelKey.SemVer2, true))
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
                var result = await controller.DisplayPackage("Foo", "1.1.1");

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("1.1.1", model.Version);
                Assert.Equal("A test package!", model.Title);
            }

            [Theory]
            [InlineData(PackageStatus.Validating)]
            [InlineData(PackageStatus.FailedValidation)]
            public async Task GivenAValidatingPackageThatTheCurrentUserOwnsThenShowIt(PackageStatus packageStatus)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    p => p.PackageRegistration.Owners.Add(TestUtility.FakeUser));

                // Assert
                Assert.IsType<ViewResult>(result);
            }

            [Theory]
            [InlineData(PackageStatus.Validating)]
            [InlineData(PackageStatus.FailedValidation)]
            public async Task GivenAValidatingPackageThatTheCurrentUserDoesNotOwnThenHideIt(PackageStatus packageStatus)
            {
                // Arrange & Act
                var result = await GetActionResultForPackageStatusAsync(
                    packageStatus,
                    p => p.PackageRegistration.Owners.Clear());

                // Assert
                ResultAssert.IsNotFound(result);
            }

            private async Task<ActionResult> GetActionResultForPackageStatusAsync(
                PackageStatus packageStatus,
                Action<Package> mutatePackage)
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var httpContext = new Mock<HttpContextBase>();
                var httpCachePolicy = new Mock<HttpCachePolicyBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    httpContext: httpContext);
                controller.SetCurrentUser(TestUtility.FakeUser);
                httpContext.Setup(c => c.Response.Cache).Returns(httpCachePolicy.Object);

                var package = new Package
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "NuGet.Versioning",
                        Owners = new List<User>(),
                    },
                    Version = "3.4.0",
                    NormalizedVersion = "3.4.0",
                    PackageStatusKey = packageStatus,
                };

                mutatePackage(package);

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

            [Fact]
            public async Task GivenAValidPackageThatTheCurrentUserOwnsItDisablesResponseCaching()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var editPackageService = new Mock<EditPackageService>();
                var httpContext = new Mock<HttpContextBase>();
                var httpCachePolicy = new Mock<HttpCachePolicyBase>(MockBehavior.Strict);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    editPackageService: editPackageService,
                    httpContext: httpContext);
                controller.SetCurrentUser(TestUtility.FakeUser);
                httpContext.Setup(c => c.Response.Cache).Returns(httpCachePolicy.Object);

                httpCachePolicy.Setup(c => c.SetCacheability(HttpCacheability.NoCache)).Verifiable();
                httpCachePolicy.Setup(c => c.SetNoStore()).Verifiable();
                httpCachePolicy.Setup(c => c.SetMaxAge(TimeSpan.Zero)).Verifiable();
                httpCachePolicy.Setup(c => c.SetRevalidation(HttpCacheRevalidation.AllCaches)).Verifiable();

                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo",
                        Owners = new List<User>() { TestUtility.FakeUser }
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!"
                };

                packageService
                    .Setup(p => p.FindPackageByIdAndVersion("Foo", "1.1.1", SemVerLevelKey.SemVer2, true))
                    .Returns(package);

                // Act
                await controller.DisplayPackage("Foo", "1.1.1");

                // Assert
                httpCachePolicy.VerifyAll();
            }

            [Fact]
            public async Task GivenAValidPackageThatTheCurrentUserOwnsWithNoEditsItDisplaysCurrentMetadata()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var editPackageService = new Mock<EditPackageService>();
                var httpContext = new Mock<HttpContextBase>();
                var httpCachePolicy = new Mock<HttpCachePolicyBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    editPackageService: editPackageService,
                    indexingService: indexingService,
                    httpContext: httpContext);
                controller.SetCurrentUser(TestUtility.FakeUser);
                httpContext.Setup(c => c.Response.Cache).Returns(httpCachePolicy.Object);

                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo",
                        Owners = new List<User>() { TestUtility.FakeUser }
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!"
                };

                packageService
                    .Setup(p => p.FindPackageByIdAndVersion("Foo", "1.1.1", SemVerLevelKey.SemVer2, true))
                    .Returns(package);
                editPackageService
                    .Setup(e => e.GetPendingMetadata(package))
                    .ReturnsNull();

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                // Act
                var result = await controller.DisplayPackage("Foo", "1.1.1");

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("1.1.1", model.Version);
                Assert.Equal("A test package!", model.Title);
            }

            [Fact]
            public async Task GivenAValidPackageThatTheCurrentUserOwnsWithEditsItDisplaysEditedMetadata()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var indexingService = new Mock<IIndexingService>();
                var editPackageService = new Mock<EditPackageService>();
                var httpContext = new Mock<HttpContextBase>();
                var httpCachePolicy = new Mock<HttpCachePolicyBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    editPackageService: editPackageService,
                    indexingService: indexingService,
                    httpContext: httpContext);
                controller.SetCurrentUser(TestUtility.FakeUser);
                httpContext.Setup(c => c.Response.Cache).Returns(httpCachePolicy.Object);
                var package = new Package()
                {
                    PackageRegistration = new PackageRegistration()
                    {
                        Id = "Foo",
                        Owners = new List<User>() { TestUtility.FakeUser }
                    },
                    Version = "01.1.01",
                    NormalizedVersion = "1.1.1",
                    Title = "A test package!"
                };

                packageService
                    .Setup(p => p.FindPackageByIdAndVersion("Foo", "1.1.1", SemVerLevelKey.SemVer2, true))
                    .Returns(package);
                editPackageService
                    .Setup(e => e.GetPendingMetadata(package))
                    .Returns(new PackageEdit()
                    {
                        Title = "A modified package!"
                    });

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                // Act
                var result = await controller.DisplayPackage("Foo", "1.1.1");

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("1.1.1", model.Version);
                Assert.Equal("A modified package!", model.Title);
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
                Assert.Equal("", model.ReadMeHtml);
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
            public async Task WhenHasReadMeAndFileNotFound_ReturnsEmptyString()
            {
                // Arrange & Act
                var result = await GetDisplayPackageResult(null, true);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("", model.ReadMeHtml);
            }

            [Fact]
            public async Task WhenHasReadMeFalse_ReturnsEmptyString()
            {
                // Arrange and Act
                var result = await GetDisplayPackageResult(null, false);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("", model.ReadMeHtml);
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
                    fileService.Setup(f => f.DownloadReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<bool>())).Returns(Task.FromResult(readMeHtml));
                }

                return await controller.DisplayPackage("Foo", /*version*/null);
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
            [MemberData("TheOwnershipRequestMethods_Data")]
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
            [MemberData("TheOwnershipRequestMethods_Data")]
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
            [MemberData("TheOwnershipRequestMethods_Data")]
            public async Task WithOrganizationCollaboratorReturnsNotYourRequest(InvokeOwnershipRequest invokeOwnershipRequest)
            {
                // Arrange
                var package = new PackageRegistration { Id = "foo" };

                var currentUser = new User { Username = "username", Key = _key++ };

                var organization = new Organization { Key = _key++, Username = "organization", Members = new[] { new Membership { Member = currentUser, IsAdmin = false } } };

                var mockHttpContext = new Mock<HttpContextBase>();

                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById(package.Id)).Returns(package);

                var userService = new Mock<IUserService>();
                userService.Setup(x => x.FindByUsername(organization.Username)).Returns(organization);

                var controller = CreateController(
                    GetConfigurationService(),
                    httpContext: mockHttpContext,
                    packageService: packageService,
                    userService: userService);
                controller.SetCurrentUser(currentUser);
                TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                // Act
                var result = await invokeOwnershipRequest(controller, package.Id, organization.Username, "token");

                // Assert
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result, "ConfirmOwner");
                Assert.Equal(ConfirmOwnershipResult.NotYourRequest, model.Result);
                Assert.Equal(organization.Username, model.Username);
            }

            [Theory]
            [MemberData("TheOwnershipRequestMethods_Data")]
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
            [MemberData("TheOwnershipRequestMethods_Data")]
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
                var messageService = new Mock<IMessageService>();
                string sentMessage = null;
                messageService.Setup(
                    s => s.SendContactOwnersMessage(
                        It.IsAny<MailAddress>(),
                        It.IsAny<PackageRegistration>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        false))
                    .Callback<MailAddress, PackageRegistration, string, string, bool>((_, __, msg, ___, ____) => sentMessage = msg);
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
            }

            [Fact]
            public void CallsSendContactOwnersMessageWithUserInfo()
            {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(
                    s => s.SendContactOwnersMessage(
                        It.IsAny<MailAddress>(),
                        It.IsAny<PackageRegistration>(),
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
            [Fact]
            public void DisplaysFullVersionStringAndUsesNormalizedVersionsInUrlsInSelectList()
            {
                // Arrange
                var user = new User("Frodo") { Key = 1 };
                var packageRegistration = new PackageRegistration { Id = "Foo" };
                packageRegistration.Owners.Add(user);

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

                var packageDelete = new PackageDelete
                {
                    DeletedBy = user,
                    DeletedByKey = user.Key,
                    Packages = new[] { package },
                    Reason = "Other",
                    Signature = "John Doe"
                };

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0.0", SemVerLevelKey.Unknown, true))
                    .Returns(package).Verifiable();
                
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService);
                controller.SetCurrentUser(user);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                // Act
                var result = controller.Delete("Foo", "1.0.0");

                // Assert
                packageService.Verify();

                Assert.IsType<ViewResult>(result);
                var model = ((ViewResult)result).Model as DeletePackageViewModel;
                Assert.NotNull(model);

                // Verify version select list
                Assert.Equal(packageRegistration.Packages.Count, model.VersionSelectList.Count());

                foreach (var pkg in packageRegistration.Packages)
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
        }

        public class TheEditMethod
            : TestContainer
        {
            [Fact]
            public async Task UpdatesUnlistedIfSelected()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(new User("Frodo"));

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
                controller.SetCurrentUser(new User("Frodo"));
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.Edit("Foo", "1.0", listed: false, urlFactory: (pkg, relativeUrl) => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                indexingService.Verify(i => i.UpdatePackage(package));
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
            }

            [Fact]
            public async Task UpdatesUnlistedIfNotSelected()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    Listed = true
                };
                package.PackageRegistration.Owners.Add(new User("Frodo"));

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
                controller.SetCurrentUser(new User("Frodo"));
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
            public async Task UsesNormalizedVersionsInUrlsInSelectList()
            {
                // Arrange
                var user = new User("Frodo") { Key = 1 };
                var packageRegistration = new PackageRegistration { Id = "Foo" };
                packageRegistration.Owners.Add(user);

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

                var packageEdit = new PackageEdit
                {
                    PackageKey = package.Key,
                    Package = package,
                    User = user,
                    UserKey = user.Key
                };

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0.0", SemVerLevelKey.Unknown, true))
                    .Returns(package).Verifiable();
                packageService.Setup(svc => svc.FindPackageRegistrationById("Foo"))
                    .Returns(package.PackageRegistration).Verifiable();

                var editPackageService = new Mock<EditPackageService>(MockBehavior.Strict);
                editPackageService.Setup(svc => svc.GetPendingMetadata(package))
                    .Returns(packageEdit).Verifiable();

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    editPackageService: editPackageService);
                controller.SetCurrentUser(user);

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                // Act
                var result = await controller.Edit("Foo", "1.0.0");

                // Assert
                packageService.Verify();
                editPackageService.Verify();

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
            [InlineData(PackageEditReadMeState.Changed)]
            [InlineData(PackageEditReadMeState.Deleted)]
            public async Task WhenReadMeEditPending_ReturnsPending(PackageEditReadMeState readMeState)
            {
                // Arrange
                var packageEdit = new PackageEdit { ReadMeState = readMeState };

                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();

                var controller = SetupController(packageEdit: packageEdit, packageFileService: packageFileService);

                // Act.
                var result = await controller.Edit("packageId", "1.0");

                // Assert.
                var model = ResultAssert.IsView<EditPackageRequest>(result);

                Assert.NotNull(model?.Edit?.ReadMe);
                Assert.Equal("Written", model.Edit.ReadMe.SourceType);
                Assert.Equal("markdown", model.Edit.ReadMe.SourceText);

                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), true), Times.Once);
                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), false), Times.Never);
            }

            [Fact]
            public async Task WhenNoReadMeEditPending_ReturnsActive()
            {
                // Arrange
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();

                var controller = SetupController(hasReadMe: true, packageFileService: packageFileService);

                // Act.
                var result = await controller.Edit("packageId", "1.0");

                // Assert.
                var model = ResultAssert.IsView<EditPackageRequest>(result);

                Assert.NotNull(model?.Edit?.ReadMe);
                Assert.Equal("Written", model.Edit.ReadMe.SourceType);
                Assert.Equal("markdown", model.Edit.ReadMe.SourceText);

                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), false), Times.Once);
                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), true), Times.Never);
            }

            [Fact]
            public async Task WhenNoReadMe_ReturnsNull()
            {
                // Arrange
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();

                var controller = SetupController(packageFileService: packageFileService);

                // Act.
                var result = await controller.Edit("packageId", "1.0");

                // Assert.
                var model = ResultAssert.IsView<EditPackageRequest>(result);

                Assert.NotNull(model?.Edit?.ReadMe);
                Assert.Null(model.Edit.ReadMe.SourceType);
                Assert.Null(model.Edit.ReadMe.SourceText);

                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<bool>()), Times.Never);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task OnPostBackWithReadMe_SavesPending(bool hasReadMe)
            {
                // Arrange
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();
                packageFileService.Setup(s => s.SavePendingReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<string>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var controller = SetupController(hasReadMe: hasReadMe, packageFileService: packageFileService);

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
                packageFileService.Verify(s => s.SavePendingReadMeMdFileAsync(It.IsAny<Package>(), "markdown2"));

                // Verify that a comparison was done against the active readme.
                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), false), Times.Exactly(hasReadMe ? 1 : 0));
                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), true), Times.Never);
            }

            [Fact]
            public async Task OnPostBackWithNoReadMe_DeletesPending()
            {
                // Arrange
                var packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult("markdown"))
                    .Verifiable();
                packageFileService.Setup(s => s.DeleteReadMeMdFileAsync(It.IsAny<Package>(), It.IsAny<bool>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

                var controller = SetupController(hasReadMe: true, packageFileService: packageFileService);

                var formData = new VerifyPackageRequest
                {
                    Edit = new EditPackageVersionReadMeRequest()
                };

                // Act.
                var result = await controller.Edit("packageId", "1.0", formData, "returnUrl");

                // Assert.
                packageFileService.Verify(s => s.DeleteReadMeMdFileAsync(It.IsAny<Package>(), true));

                // Verify that a comparison was done against the active readme.
                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), false), Times.Once);
                packageFileService.Verify(s => s.DownloadReadMeMdFileAsync(It.IsAny<Package>(), true), Times.Never);
            }

            private PackagesController SetupController(bool hasReadMe = false, PackageEdit packageEdit = null,
                Mock<IPackageFileService> packageFileService = null)
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "packageId" },
                    Version = "1.0",
                    Listed = true,
                    HasReadMe = hasReadMe
                };
                package.PackageRegistration.Owners.Add(new User("user"));

                var packageService = new Mock<IPackageService>();
                packageService.Setup(s => s.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>()))
                    .Returns(package);
                packageService.Setup(s => s.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(package.PackageRegistration);

                var editPackageService = new Mock<EditPackageService>();
                editPackageService.Setup(s => s.GetPendingMetadata(It.IsAny<Package>()))
                    .Returns(packageEdit);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    editPackageService: editPackageService,
                    packageFileService: packageFileService);
                controller.SetCurrentUser(new User("user"));

                var routeCollection = new RouteCollection();
                Routes.RegisterRoutes(routeCollection);
                controller.Url = new UrlHelper(controller.ControllerContext.RequestContext, routeCollection);

                return controller;
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

        public class TheReportAbuseMethod
            : TestContainer
        {
            [Fact]
            public async Task SendsMessageToGalleryOwnerWithEmailOnlyWhenUnauthenticated()
            {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(
                    s => s.ReportAbuse(It.Is<ReportPackageRequest>(r => r.Message == "Mordor took my finger")));
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "mordor" },
                    Version = "2.0.1"
                };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict("mordor", "2.0.1")).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    messageService: messageService,
                    httpContext: httpContext);
                var model = new ReportAbuseViewModel
                {
                    Email = "frodo@hobbiton.example.com",
                    Message = "Mordor took my finger.",
                    Reason = ReportPackageReason.ViolatesALicenseIOwn,
                    AlreadyContactedOwner = true,
                    Signature = "Frodo"
                };

                TestUtility.SetupUrlHelper(controller, httpContext);
                var result = await controller.ReportAbuse("mordor", "2.0.1", model) as RedirectResult;

                Assert.NotNull(result);
                messageService.Verify(
                    s => s.ReportAbuse(
                        It.Is<ReportPackageRequest>(
                            r => r.FromAddress.Address == "frodo@hobbiton.example.com"
                                 && r.Package == package
                                 && r.Reason == EnumHelper.GetDescription(ReportPackageReason.ViolatesALicenseIOwn)
                                 && r.Message == "Mordor took my finger."
                                 && r.AlreadyContactedOwners)));
            }

            [Fact]
            public async Task SendsMessageToGalleryOwnerWithUserInfoWhenAuthenticated()
            {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(
                    s => s.ReportAbuse(It.Is<ReportPackageRequest>(r => r.Message == "Mordor took my finger")));
                var user = new User { EmailAddress = "frodo@hobbiton.example.com", Username = "Frodo", Key = 1 };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "mordor" },
                    Version = "2.0.1"
                };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict("mordor", It.IsAny<string>())).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    messageService: messageService,
                    httpContext: httpContext);
                controller.SetCurrentUser(user);
                var model = new ReportAbuseViewModel
                {
                    Message = "Mordor took my finger",
                    Reason = ReportPackageReason.ViolatesALicenseIOwn,
                    Signature = "Frodo"
                };

                TestUtility.SetupUrlHelper(controller, httpContext);
                ActionResult result = await controller.ReportAbuse("mordor", "2.0.1", model) as RedirectResult;

                Assert.NotNull(result);
                messageService.Verify(
                    s => s.ReportAbuse(
                        It.Is<ReportPackageRequest>(
                            r => r.Message == "Mordor took my finger"
                                 && r.FromAddress.Address == "frodo@hobbiton.example.com"
                                 && r.FromAddress.DisplayName == "Frodo"
                                 && r.Reason == EnumHelper.GetDescription(ReportPackageReason.ViolatesALicenseIOwn))));
            }

            [Fact]
            public void FormRedirectsPackageOwnerToReportMyPackage()
            {
                var user = new User { EmailAddress = "darklord@mordor.com", Username = "Sauron" };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Mordor", Owners = { user } },
                    Version = "2.0.1"
                };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict("Mordor", It.IsAny<string>())).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    httpContext: httpContext);
                controller.SetCurrentUser(user);

                TestUtility.SetupUrlHelper(controller, httpContext);
                ActionResult result = controller.ReportAbuse("Mordor", "2.0.1");
                Assert.IsType<RedirectToRouteResult>(result);
                Assert.Equal("ReportMyPackage", ((RedirectToRouteResult)result).RouteValues["Action"]);
            }

            [Fact]
            public async Task HtmlEncodesMessageContent()
            {
                var messageService = new Mock<IMessageService>();
                messageService.Setup(
                    s => s.ReportAbuse(It.Is<ReportPackageRequest>(r => r.Message == "Mordor took my finger")));
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "mordor" },
                    Version = "2.0.1"
                };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict("mordor", "2.0.1")).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.Request.IsAuthenticated).Returns(false);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: packageService,
                    messageService: messageService,
                    httpContext: httpContext);
                var model = new ReportAbuseViewModel
                {
                    Email = "frodo@hobbiton.example.com",
                    Message = "I like the cut of your jib. It's <b>bold</b>.",
                    Reason = ReportPackageReason.ViolatesALicenseIOwn,
                    AlreadyContactedOwner = true,
                    Signature = "Frodo"
                };

                TestUtility.SetupUrlHelper(controller, httpContext);
                await controller.ReportAbuse("mordor", "2.0.1", model);

                messageService.Verify(
                    s => s.ReportAbuse(
                        It.Is<ReportPackageRequest>(
                            r => r.FromAddress.Address == "frodo@hobbiton.example.com"
                                 && r.Package == package
                                 && r.Reason == EnumHelper.GetDescription(ReportPackageReason.ViolatesALicenseIOwn)
                                 && r.Message == "I like the cut of your jib. It&#39;s &lt;b&gt;bold&lt;/b&gt;."
                                 && r.AlreadyContactedOwners)));
            }
        }

        public class TheReportMyPackageMethod
            : TestContainer
        {
            private Package _package;
            private ReportMyPackageViewModel _viewModel;
            private Issue _supportRequest;
            private User _user;
            private Mock<IPackageService> _packageService;
            private Mock<IMessageService> _messageService;
            private Mock<IPackageDeleteService> _packageDeleteService;
            private Mock<ISupportRequestService> _supportRequestService;
            private PackagesController _controller;

            public TheReportMyPackageMethod()
            {
                _user = new User
                {
                    EmailAddress = "frodo@hobbiton.example.com",
                    Username = "Frodo",
                    Key = 2
                };
                _package = new Package
                {
                    PackageRegistration = new PackageRegistration
                    {
                        Id = "Mordor",
                        Owners = { _user },
                    },
                    Version = "2.00.1",
                    NormalizedVersion = "2.0.1",
                };
                _viewModel = new ReportMyPackageViewModel
                {
                    Reason = ReportPackageReason.ContainsPrivateAndConfidentialData,
                    Message = "Message!",
                };
                _supportRequest = new Issue
                {
                    Key = 23,
                };

                _packageService = new Mock<IPackageService>();
                _packageService
                    .Setup(p => p.FindPackageByIdAndVersionStrict(_package.PackageRegistration.Id, _package.Version))
                    .Returns(_package);

                _messageService = new Mock<IMessageService>();
                _packageDeleteService = new Mock<IPackageDeleteService>();

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
                _controller.SetCurrentUser(_user);

                TestUtility.SetupUrlHelper(_controller, httpContext);
            }

            [Fact]
            public async Task GetRedirectsNonOwnersToReportAbuse()
            {
                await RedirectsNonOwnersToReportAbuse(
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version));
            }

            [Fact]
            public async Task PostRedirectsNonOwnersToReportAbuse()
            {
                await RedirectsNonOwnersToReportAbuse(
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version,
                        _viewModel));
            }

            private async Task RedirectsNonOwnersToReportAbuse(Func<Task<ActionResult>> actAsync)
            {
                // Arrange
                _package.PackageRegistration.Owners.Clear();

                // Act
                var result = await actAsync();

                // Assert
                Assert.IsType<RedirectToRouteResult>(result);
                Assert.Equal("ReportAbuse", ((RedirectToRouteResult)result).RouteValues["Action"]);
            }


            [Fact]
            public async Task GetRedirectsMissingPackageToNotFound()
            {
                await RedirectsMissingPackageToNotFound(
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version));
            }

            [Fact]
            public async Task PostRedirectsMissingPackageToNotFound()
            {
                await RedirectsMissingPackageToNotFound(
                    () => _controller.ReportMyPackage(
                        _package.PackageRegistration.Id,
                        _package.Version,
                        _viewModel));
            }

            private async Task RedirectsMissingPackageToNotFound(Func<Task<ActionResult>> actAsync)
            {
                // Arrange
                _packageService
                    .Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns<Package>(null);

                // Act
                var result = await actAsync();

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public async Task HtmlEncodesMessageContent()
            {
                // Arrange
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
                Assert.Equal(_user.EmailAddress, reportRequest.FromAddress.Address);
                Assert.Same(_package, reportRequest.Package);
                Assert.Equal(EnumHelper.GetDescription(ReportPackageReason.ViolatesALicenseIOwn), reportRequest.Reason);
                Assert.Equal("I like the cut of your jib. It&#39;s &lt;b&gt;bold&lt;/b&gt;.", reportRequest.Message);
            }

            [Fact]
            public async Task DoesNotCheckDeleteAllowedIfDeleteWasNotRequested()
            {
                // Arrange
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
                        _user.EmailAddress,
                        EnumHelper.GetDescription(_viewModel.Reason.Value),
                        _user,
                        _package),
                    Times.Once);
                Assert.Equal(Strings.SupportRequestSentTransientMessage, _controller.TempData["Message"]);
            }

            [Fact]
            public async Task AllowsPackageDelete()
            {
                // Arrange
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
                        _user.EmailAddress,
                        EnumHelper.GetDescription(_viewModel.Reason.Value),
                        _user,
                        _package),
                    Times.Once);
                _packageDeleteService.Verify(
                    x => x.SoftDeletePackagesAsync(
                        It.Is<IEnumerable<Package>>(p => p.First() == _package),
                        _user,
                        EnumHelper.GetDescription(_viewModel.Reason.Value),
                        Strings.UserPackageDeleteSignature),
                    Times.Once);
                _supportRequestService.Verify(
                    x => x.UpdateIssueAsync(
                        _supportRequest.Key,
                        null,
                        IssueStatusKeys.Resolved,
                        null,
                        _user.Username),
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

            [Fact]
            public async Task TreatsDeleteFailureAsNormalRequest()
            {
                // Arrange
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
                        _user,
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

            [Fact]
            public async Task RequiresMessageWhenNotDeleting()
            {
                // Arrange
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

            [Fact]
            public async Task RequiresConfirmationWhenDeleting()
            {
                // Arrange
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

            [Theory]
            [InlineData(ReportPackageReason.ContainsMaliciousCode)]
            [InlineData(ReportPackageReason.ContainsPrivateAndConfidentialData)]
            [InlineData(ReportPackageReason.ReleasedInPublicByAccident)]
            public async Task RequiresDeleteDecision(ReportPackageReason reason)
            {
                // Arrange
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
            
            [Theory]
            [InlineData(ReportPackageReason.Other)]
            public async Task DoesNotRequireDeleteDecision(ReportPackageReason reason)
            {
                // Arrange
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

            [Fact]
            public async Task IgnoresDeleteRequestWhenNotAllowed()
            {
                // Arrange
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
            [Fact]
            public async Task WillRedirectToVerifyPackageActionWhenThereIsAlreadyAnUploadInProgress()
            {
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakeUploadFileService = new Mock<IUploadFileService>();
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult<Stream>(fakeFileStream));
                    var controller = CreateController(
                        GetConfigurationService(),
                        uploadFileService: fakeUploadFileService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = (await controller.UploadPackage() as ViewResult).Model as SubmitPackageRequest;

                    Assert.NotNull(result);
                    Assert.True(result.IsUploadInProgress);
                    Assert.NotNull(result.InProgressUpload);
                }
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

            [Fact]
            public async Task WillShowTheViewWithErrorsWhenThePackageIdIsAlreadyBeingUsed()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakePackageRegistration = new PackageRegistration { Id = "theId", Owners = new[] { new User { Key = 1 /* not the current user */ } } };
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(fakePackageRegistration);
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: fakePackageService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(String.Format(Strings.PackageIdNotAvailable, "theId"), controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillShowTheViewWithErrorsWhenThePackageIdMatchesNonOwnedNamespace()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("Random.Package1", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(() => null);

                var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                var testNamespace = new ReservedNamespace("random.", isSharedNamespace: false, isPrefix: true);
                var user1 = new User { Key = 1, Username = "random1" };
                testNamespace.Owners.Add(user1);
                IReadOnlyCollection<ReservedNamespace> matchingNamespaces = new List<ReservedNamespace> { testNamespace };
                fakeReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(matchingNamespaces);
                var fakeTelemetryService = new Mock<ITelemetryService>();

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageService: fakePackageService,
                    fakeNuGetPackage: fakeFileStream,
                    reservedNamespaceService: fakeReservedNamespaceService,
                    telemetryService: fakeTelemetryService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(String.Format(Strings.UploadPackage_IdNamespaceConflict), controller.ModelState[String.Empty].Errors[0].ErrorMessage);
                fakeTelemetryService.Verify(x => x.TrackPackagePushNamespaceConflictEvent(It.IsAny<string>(), It.IsAny<string>(), TestUtility.FakeUser, controller.OwinContext.Request.User.Identity), Times.Once);
            }

            [Fact]
            public async Task WillUploadThePackageWhenIdMatchesSharedNamespace()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("Random.Package1", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult<Stream>(TestPackage.CreateTestPackageStream("Random.Package1", "1.0.0")));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(() => null);

                var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                var testNamespace = new ReservedNamespace("random.", isSharedNamespace: true, isPrefix: true);
                var user1 = new User { Key = 1, Username = "random1" };
                testNamespace.Owners.Add(user1);
                IReadOnlyCollection<ReservedNamespace> matchingNamespaces = new List<ReservedNamespace> { testNamespace };
                fakeReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(matchingNamespaces);

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageService: fakePackageService,
                    fakeNuGetPackage: fakeFileStream,
                    reservedNamespaceService: fakeReservedNamespaceService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.UploadPackage(fakeUploadedFile.Object);

                Assert.True(controller.ModelState.IsValid);
                fakeUploadFileService.Verify(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, fakeFileStream));
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillUploadThePackageWhenIdMatchesOwnedNamespace()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("Random.Package1", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);

                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.SetupSequence(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                    .Returns(Task.FromResult<Stream>(null))
                    .Returns(Task.FromResult<Stream>(TestPackage.CreateTestPackageStream("Random.Package1", "1.0.0")));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(() => null);

                var fakeReservedNamespaceService = new Mock<IReservedNamespaceService>();
                var testNamespace = new ReservedNamespace("random.", isSharedNamespace: true, isPrefix: true);
                testNamespace.Owners.Add(TestUtility.FakeUser);

                IReadOnlyCollection<ReservedNamespace> matchingNamespaces = new List<ReservedNamespace> { testNamespace };
                fakeReservedNamespaceService
                    .Setup(r => r.GetReservedNamespacesForId(It.IsAny<string>()))
                    .Returns(matchingNamespaces);

                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService,
                    packageService: fakePackageService,
                    fakeNuGetPackage: fakeFileStream,
                    reservedNamespaceService: fakeReservedNamespaceService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.UploadPackage(fakeUploadedFile.Object);

                Assert.True(controller.ModelState.IsValid);
                fakeUploadFileService.Verify(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, fakeFileStream));
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillShowTheViewWithErrorsWhenThePackageAlreadyExists()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageByIdAndVersionStrict(It.IsAny<string>(), It.IsAny<string>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "1.0.0" });
                var controller = CreateController(
                    GetConfigurationService(),
                    packageService: fakePackageService);
                controller.SetCurrentUser(TestUtility.FakeUser);

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
                    .Returns(Task.FromResult<Stream>(TestPackage.CreateTestPackageStream("thePackageId", "1.0.0")));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                var controller = CreateController(
                    GetConfigurationService(),
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.True(result.Data is VerifyPackageRequest);
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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    fakePackageUploadService
                        .Setup(x => x.CommitPackageAsync(
                            It.IsAny<Package>(),
                            It.IsAny<Stream>()))
                        .ReturnsAsync(PackageCommitResult.Conflict);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null });

                    var jsonResult = Assert.IsType<JsonResult>(result);
                    Assert.Equal((int)HttpStatusCode.Conflict, controller.Response.StatusCode);
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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    fakePackageUploadService
                        .Setup(x => x.CommitPackageAsync(
                            It.IsAny<Package>(),
                            It.IsAny<Stream>()))
                        .ReturnsAsync(commitResult);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null });

                    fakePackageUploadService.Verify(x => x.CommitPackageAsync(
                        It.IsAny<Package>(),
                        It.IsAny<Stream>()), Times.Once);
                }
            }

            [Fact]
            public async Task WillCreateThePackage()
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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null });

                    fakePackageUploadService.Verify(x => x.GeneratePackageAsync(
                        It.IsAny<string>(),
                        It.IsAny<PackageArchiveReader>(),
                        It.IsAny<PackageStreamMetadata>(),
                        It.IsAny<User>()), Times.Once);
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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                    var fakePackageFileService = new Mock<IPackageFileService>();
                    fakePackageFileService.Setup(x => x.SavePackageFileAsync(fakePackage, It.IsAny<Stream>())).Returns(Task.FromResult(0)).Verifiable();

                    var fakeIndexingService = new Mock<IIndexingService>(MockBehavior.Strict);
                    fakeIndexingService.Setup(f => f.UpdateIndex()).Verifiable();

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        packageFileService: fakePackageFileService,
                        indexingService: fakeIndexingService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null });

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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    fakePackageService.Setup(x => x.PublishPackageAsync(fakePackage, false))
                        .Returns(Task.CompletedTask);
                    fakePackageService.Setup(x => x.MarkPackageUnlistedAsync(fakePackage, false))
                        .Returns(Task.CompletedTask);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null });

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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null });

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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null });

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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageService: fakePackageService,
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = listed.GetValueOrDefault(true), Edit = null });

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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: facePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null });

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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null });

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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(
                        new VerifyPackageRequest() { Listed = false, Edit = null }) as JsonResult;

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
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeAutoCuratePackageCmd = new Mock<IAutomaticallyCuratePackageCommand>();
                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        autoCuratePackageCmd: fakeAutoCuratePackageCmd);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null });

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
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var auditingService = new TestAuditingService();

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        auditingService: auditingService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Edit = null });

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
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                    var fakeTelemetryService = new Mock<ITelemetryService>();

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: fakePackageUploadService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        telemetryService: fakeTelemetryService);

                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Edit = null });

                    // Assert
                    fakeTelemetryService.Verify(x => x.TrackPackagePushEvent(It.IsAny<Package>(), TestUtility.FakeUser, controller.OwinContext.Request.User.Identity), Times.Once);
                }
            }

            public static IEnumerable<object[]> WillApplyEdits_Data
            {
                get
                {
                    yield return new object[] { new EditPackageVersionReadMeRequest() {
                        ReadMe = new ReadMeRequest { SourceType = "Written", SourceText = "markdown" } }
                    };
                }
            }

            [Theory]
            [MemberData("WillApplyEdits_Data")]
            public async Task WillApplyEdits(EditPackageVersionReadMeRequest edit)
            {
                // Arrange
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakeUploadFileService = new Mock<IUploadFileService>();
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.CompletedTask);

                    var packageUploadService = new Mock<IPackageUploadService>();

                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "thePackageId" }, Version = "1.0.0" };
                    packageUploadService
                        .Setup(x => x.GeneratePackageAsync(
                            It.IsAny<string>(),
                            It.IsAny<PackageArchiveReader>(),
                            It.IsAny<PackageStreamMetadata>(),
                            It.IsAny<User>()))
                        .Returns(Task.FromResult(fakePackage));

                    var fakeEditPackageService = new Mock<EditPackageService>();

                    var fakePackageFileService = new Mock<IPackageFileService>();
                    fakePackageFileService.Setup(x => x.SavePendingReadMeMdFileAsync(fakePackage, It.IsAny<string>())).Returns(Task.CompletedTask);

                    var controller = CreateController(
                        GetConfigurationService(),
                        packageUploadService: packageUploadService,
                        editPackageService: fakeEditPackageService,
                        uploadFileService: fakeUploadFileService,
                        packageFileService: fakePackageFileService);

                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Edit = edit });

                    // Assert 
                    fakeEditPackageService.Verify(x => x.StartEditPackageRequest(fakePackage, edit, TestUtility.FakeUser), Times.Once);

                    var hasReadMe = !string.IsNullOrEmpty(edit.ReadMe?.SourceType);
                    fakePackageFileService.Verify(x => x.SavePendingReadMeMdFileAsync(fakePackage, "markdown"), Times.Exactly(hasReadMe ? 1 : 0));
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
                        It.IsAny<User>()))
                    .Returns(Task.FromResult(fakePackage));

                var fakeEditPackageService = new Mock<EditPackageService>();

                var fakePackageFileService = new Mock<IPackageFileService>();
                fakePackageFileService.Setup(x => x.SavePendingReadMeMdFileAsync(fakePackage, It.IsAny<string>())).Returns(Task.CompletedTask);

                var controller = CreateController(
                    GetConfigurationService(),
                    packageUploadService: packageUploadService,
                    editPackageService: fakeEditPackageService,
                    uploadFileService: fakeUploadFileService,
                    packageFileService: fakePackageFileService);

                controller.SetCurrentUser(TestUtility.FakeUser);

                // Act
                await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Edit = edit });

                var hasReadMe = !string.IsNullOrEmpty(edit.ReadMe?.SourceType);
                fakePackageFileService.Verify(x => x.SavePendingReadMeMdFileAsync(fakePackage, "markdown"), Times.Exactly(hasReadMe ? 1 : 0));
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
            [Fact]
            public async Task IndexingAndPackageServicesAreUpdated()
            {
                // Arrange
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Foo" },
                    Version = "1.0",
                    HideLicenseReport = true
                };
                package.PackageRegistration.Owners.Add(new User("Smeagol"));

                var packageService = new Mock<IPackageService>(MockBehavior.Strict);
                packageService.Setup(svc => svc.SetLicenseReportVisibilityAsync(It.IsAny<Package>(), It.Is<bool>(t => t == true), It.IsAny<bool>()))
                    .Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.SetLicenseReportVisibilityAsync(It.IsAny<Package>(), It.Is<bool>(t => t == false), It.IsAny<bool>()))
                    .Returns(Task.CompletedTask).Verifiable();
                packageService.Setup(svc => svc.FindPackageByIdAndVersionStrict("Foo", "1.0"))
                    .Returns(package).Verifiable();

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.Request.IsAuthenticated).Returns(true);
                httpContext.Setup(h => h.User.Identity.Name).Returns("Smeagol");

                var indexingService = new Mock<IIndexingService>();

                var controller = CreateController(
                        GetConfigurationService(),
                        packageService: packageService,
                        httpContext: httpContext,
                        indexingService: indexingService);
                controller.SetCurrentUser(new User("Smeagol"));
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.SetLicenseReportVisibility("Foo", "1.0", visible: false, urlFactory: (pkg, relativeUrl) => @"~\Bar.cshtml");

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

