// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet.Packaging;
using NuGetGallery.Areas.Admin;
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
    {
        private static PackagesController CreateController(
            Mock<IPackageService> packageService = null,
            Mock<IUploadFileService> uploadFileService = null,
            Mock<IMessageService> messageService = null,
            Mock<HttpContextBase> httpContext = null,
            Mock<EditPackageService> editPackageService = null,
            Stream fakeNuGetPackage = null,
            Mock<ISearchService> searchService = null,
            Exception readPackageException = null,
            Mock<IAutomaticallyCuratePackageCommand> autoCuratePackageCmd = null,
            Mock<IAppConfiguration> config = null,
            Mock<IPackageFileService> packageFileService = null,
            Mock<IEntitiesContext> entitiesContext = null,
            Mock<IIndexingService> indexingService = null,
            Mock<ICacheService> cacheService = null,
            Mock<IPackageDeleteService> packageDeleteService = null,
            Mock<ISupportRequestService> supportRequestService = null,
            IAuditingService auditingService = null,
            Mock<ITelemetryService> telemetryService = null,
            Mock<ISecurityPolicyService> securityPolicyService = null)
        {
            packageService = packageService ?? new Mock<IPackageService>();
            if (uploadFileService == null)
            {
                uploadFileService = new Mock<IUploadFileService>();
                uploadFileService.Setup(x => x.DeleteUploadFileAsync(It.IsAny<int>())).Returns(Task.FromResult(0));
                uploadFileService.Setup(x => x.GetUploadFileAsync(42)).Returns(Task.FromResult<Stream>(null));
                uploadFileService.Setup(x => x.SaveUploadFileAsync(42, It.IsAny<Stream>())).Returns(Task.FromResult(0));
            }
            messageService = messageService ?? new Mock<IMessageService>();
            searchService = searchService ?? CreateSearchService();
            autoCuratePackageCmd = autoCuratePackageCmd ?? new Mock<IAutomaticallyCuratePackageCommand>();
            config = config ?? new Mock<IAppConfiguration>();
            config.Setup(c => c.GalleryOwner).Returns(new MailAddress("support@example.com"));

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

            var controller = new Mock<PackagesController>(
                packageService.Object,
                uploadFileService.Object,
                messageService.Object,
                searchService.Object,
                autoCuratePackageCmd.Object,
                packageFileService.Object,
                entitiesContext.Object,
                config.Object,
                indexingService.Object,
                cacheService.Object,
                editPackageService.Object,
                packageDeleteService.Object,
                supportRequestService.Object,
                auditingService,
                telemetryService.Object,
                securityPolicyService.Object);

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
        {
            [Fact]
            public async Task DeletesTheInProgressPackageUpload()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(42)).Returns(Task.FromResult(0));
                var controller = CreateController(
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
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.CancelUpload();

                Assert.IsType<JsonResult>(result);
                Assert.Null(result.Data);
            }
        }

        public class TheDisplayPackageMethod
        {
            [Fact]
            public async Task GivenANonNormalizedVersionIt302sToTheNormalizedVersion()
            {
                // Arrange
                var controller = CreateController();

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
                var controller = CreateController(packageService: packageService);

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
                    packageService: packageService, indexingService: indexingService);
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

            [Fact]
            public async Task GivenAValidPackageThatTheCurrentUserOwnsItDisablesResponseCaching()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var editPackageService = new Mock<EditPackageService>();
                var httpContext = new Mock<HttpContextBase>();
                var httpCachePolicy = new Mock<HttpCachePolicyBase>(MockBehavior.Strict);
                var controller = CreateController(
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
                        Version = "2.0.0a",
                        NormalizedVersion = "2.0.0a",
                        IsLatest = true,
                        Title = "A test package!"
                    });
               

                indexingService.Setup(i => i.GetLastWriteTime()).Returns(Task.FromResult((DateTime?)DateTime.UtcNow));

                // Act
                var result = await controller.DisplayPackage("Foo", Constants.AbsoluteLatestUrlString);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("2.0.0a", model.Version);
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
                    packageService: packageService, indexingService: indexingService);
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
            public async Task GivenAValidPackageWithReadMeItDisplaysReadMe()
            {
                // Arrange
                var readMeStream = new MemoryStream(Encoding.UTF8.GetBytes("<p>Hello World!</p>"));
                
                // Act
                var result = await GetDisplayPackageResultWithReadMeStream(readMeStream, true);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("<p>Hello World!</p>", model.ReadMeHtml);
            }

            [Fact]
            public async Task GivenAPackageWithReadMeHandlesFailedDownload()
            {
                // Arrange & Act
                var result = await GetDisplayPackageResultWithReadMeStream((Stream)null, true);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Null(model.ReadMeHtml);
            }

            [Fact]
            public async Task GivenAPackageWithNoReadMeShowsThatReadMeIsEmpty()
            {
                // Arrange and Act
                var result = await GetDisplayPackageResultWithReadMeStream((Stream)null, false);

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Null(model.ReadMeHtml);
            }
        }

        public class TheConfirmOwnerMethod : TestContainer
        {
            [Fact]
            public async Task WithEmptyTokenReturnsHttpNotFound()
            {
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(new PackageRegistration());
                var controller = CreateController(packageService: packageService);
                controller.SetCurrentUser(new User { Username = "username" });

                var result = await controller.ConfirmOwner("foo", "username", "");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public async Task WithIdentityNotMatchingUserInRequestReturnsViewWithMessage()
            {
                var controller = CreateController();
                controller.SetCurrentUser(new User("userA"));
                var result = await controller.ConfirmOwner("foo", "userB", "token");

                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result);
                Assert.Equal(ConfirmOwnershipResult.NotYourRequest, model.Result);
                Assert.Equal("userB", model.Username);
            }

            [Fact]
            public async Task WithNonExistentPackageIdReturnsHttpNotFound()
            {
                // Arrange
                var controller = CreateController();
                controller.SetCurrentUser(new User { Username = "username" });

                // Act
                var result = await controller.ConfirmOwner("foo", "username", "token");

                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public async Task WithOwnerReturnsAlreadyOwnerResult()
            {
                // Arrange
                var package = new PackageRegistration { Id = "foo" };
                var user = new User { Username = "username" };
                package.Owners.Add(user);
                var mockHttpContext = new Mock<HttpContextBase>();
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(package);
                var controller = CreateController(httpContext: mockHttpContext, packageService: packageService);
                controller.SetCurrentUser(user);
                TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                // Act
                var result = await controller.ConfirmOwner("foo", "username", "token");

                // Assert
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result);
                Assert.Equal(ConfirmOwnershipResult.AlreadyOwner, model.Result);
            }

            [Theory]
            [InlineData(true)]
            [InlineData(false)]
            public async Task ReturnsSuccessIfTokenIsValid(bool tokenValid)
            {
                // Arrange
                var package = new PackageRegistration { Id = "foo" };
                var user = new User { Username = "username" };
                var mockHttpContext = new Mock<HttpContextBase>();
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(package);
                packageService.Setup(p => p.IsValidPackageOwnerRequest(package, user, "token"))
                    .Returns(tokenValid);
                packageService.Setup(p => p.AddPackageOwnerAsync(package, user)).Returns(Task.CompletedTask).Verifiable();
                var controller = CreateController(httpContext: mockHttpContext, packageService: packageService);
                controller.SetCurrentUser(user);
                TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                // Act
                var result = await controller.ConfirmOwner("foo", "username", "token");

                // Assert
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result);
                var expectedResult = tokenValid ? ConfirmOwnershipResult.Success : ConfirmOwnershipResult.Failure;
                Assert.Equal(expectedResult, model.Result);
                Assert.Equal("foo", model.PackageId);
                packageService.Verify(p => p.AddPackageOwnerAsync(package, user), tokenValid ? Times.Once() : Times.Never());
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
                    var policyMessages = await AssertConfirmOwnerSubscribesUser(fakes, fakes.Owner, fakes.ShaUser);
                    Assert.Equal(3, policyMessages.Count);

                    // subscribed notification
                    Assert.StartsWith("Owner(s) 'testUser' has (have) the following requirements that are now enforced for your account:",
                        policyMessages[fakes.Owner.Username]);
                    Assert.StartsWith("Owner(s) 'testUser' has (have) the following requirements that are now enforced for your account:",
                        policyMessages[fakes.ShaUser.Username]);

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
                    Assert.Equal(2, policyMessages.Count);
                    Assert.StartsWith("Owner(s) 'testPackageOwner' has (have) the following requirements that are now enforced for co-owner(s) 'testUser':",
                        policyMessages[fakes.Owner.Username]);
                    Assert.Equal("", policyMessages[fakes.ShaUser.Username]);
                }

                private async Task<IDictionary<string, string>> AssertConfirmOwnerSubscribesUser(Fakes fakes, params User[] usersSubscribed)
                {
                    // Arrange
                    var mockHttpContext = new Mock<HttpContextBase>();

                    var packageService = new Mock<IPackageService>();
                    packageService.Setup(p => p.FindPackageRegistrationById(It.IsAny<string>())).Returns(fakes.Package);
                    packageService.Setup(p => p.IsValidPackageOwnerRequest(fakes.Package, fakes.User, "token")).Returns(true);

                    var policyService = new Mock<ISecurityPolicyService>();
                    foreach (var user in usersSubscribed)
                    {
                        policyService.Setup(s => s.SubscribeAsync(user, "SecurePush"))
                            .Returns(Task.FromResult(true))
                            .Verifiable();
                    }

                    var policyMessages = new Dictionary<string, string>();
                    var messageService = new Mock<IMessageService>();
                    messageService.Setup(s => s.SendPackageOwnerAddedNotice(
                        It.IsAny<User>(), It.IsAny<User>(), It.IsAny<PackageRegistration>(), It.IsAny<string>(), It.IsAny<string>()))
                        .Callback<User, User, PackageRegistration, string, string>((toUser, newOwner, pkg, pkgUrl, policyMessage) =>
                        {
                            policyMessages.Add(toUser.Username, policyMessage);
                        });

                    var controller = CreateController(
                        httpContext: mockHttpContext,
                        packageService: packageService,
                        messageService: messageService,
                        securityPolicyService: policyService);

                    controller.SetCurrentUser(fakes.User);
                    TestUtility.SetupHttpContextMockForUrlGeneration(mockHttpContext, controller);

                    // Act
                    await controller.ConfirmOwner(fakes.Package.Id, fakes.User.Username, "token");

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
        {
            [Fact]
            public void OnlyShowsOwnersWhoAllowReceivingEmails()
            {
                var package = new Package {
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
                var controller = CreateController(packageService: packageService);

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

        public class TheEditMethod
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

                var controller = CreateController(packageService: packageService, indexingService: indexingService);
                controller.SetCurrentUser(new User("Frodo"));
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.Edit("Foo", "1.0", listed: false, urlFactory: p => @"~\Bar.cshtml");

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

                var controller = CreateController(packageService: packageService, indexingService: indexingService);
                controller.SetCurrentUser(new User("Frodo"));
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.Edit("Foo", "1.0", listed: true, urlFactory: p => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                indexingService.Verify(i => i.UpdatePackage(package));
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
            }
        }

        public class TheListPackagesMethod
        {
            [Fact]
            public async Task TrimsSearchTerm()
            {
                var searchService = new Mock<ISearchService>();
                searchService.Setup(s => s.Search(It.IsAny<SearchFilter>())).Returns(
                    Task.FromResult(new SearchResults(0, DateTime.UtcNow)));
                var controller = CreateController(searchService: searchService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = (await controller.ListPackages(new PackageListSearchViewModel() { Q = " test "})) as ViewResult;

                var model = result.Model as PackageListViewModel;
                Assert.Equal("test", model.SearchTerm);
            }
        }

        public class TheReportAbuseMethod
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
                    packageService: packageService,
                    messageService: messageService,
                    httpContext: httpContext);
                var model = new ReportAbuseViewModel
                    {
                        Email = "frodo@hobbiton.example.com",
                        Message = "Mordor took my finger.",
                        Reason = ReportPackageReason.IsFraudulent,
                        AlreadyContactedOwner = true,
                    };

                TestUtility.SetupUrlHelper(controller, httpContext);
                var result = await controller.ReportAbuse("mordor", "2.0.1", model) as RedirectResult;

                Assert.NotNull(result);
                messageService.Verify(
                    s => s.ReportAbuse(
                        It.Is<ReportPackageRequest>(
                            r => r.FromAddress.Address == "frodo@hobbiton.example.com"
                                 && r.Package == package
                                 && r.Reason == EnumHelper.GetDescription(ReportPackageReason.IsFraudulent)
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
                    packageService: packageService,
                    messageService: messageService,
                    httpContext: httpContext);
                controller.SetCurrentUser(user);
                var model = new ReportAbuseViewModel
                    {
                        Message = "Mordor took my finger",
                        Reason = ReportPackageReason.IsFraudulent
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
                                 && r.Reason == EnumHelper.GetDescription(ReportPackageReason.IsFraudulent))));
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
                    packageService: packageService,
                    messageService: messageService,
                    httpContext: httpContext);
                var model = new ReportAbuseViewModel
                {
                    Email = "frodo@hobbiton.example.com",
                    Message = "I like the cut of your jib. It's <b>bold</b>.",
                    Reason = ReportPackageReason.IsFraudulent,
                    AlreadyContactedOwner = true,
                };

                TestUtility.SetupUrlHelper(controller, httpContext);
                await controller.ReportAbuse("mordor", "2.0.1", model);

                messageService.Verify(
                    s => s.ReportAbuse(
                        It.Is<ReportPackageRequest>(
                            r => r.FromAddress.Address == "frodo@hobbiton.example.com"
                                 && r.Package == package
                                 && r.Reason == EnumHelper.GetDescription(ReportPackageReason.IsFraudulent)
                                 && r.Message == "I like the cut of your jib. It&#39;s &lt;b&gt;bold&lt;/b&gt;."
                                 && r.AlreadyContactedOwners)));
            }
        }

        public class TheReportMyPackageMethod
        {
            [Fact]
            public void FormRedirectsNonOwnersToReportAbuse()
            {
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "Mordor", Owners = { new User { Username = "Sauron", Key = 1 } } },
                    Version = "2.0.1"
                };
                var user = new User { EmailAddress = "frodo@hobbiton.example.com", Username = "Frodo", Key = 2 };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict("Mordor", It.IsAny<string>())).Returns(package);
                var httpContext = new Mock<HttpContextBase>();
                var controller = CreateController(
                    packageService: packageService,
                    httpContext: httpContext);
                controller.SetCurrentUser(user);

                TestUtility.SetupUrlHelper(controller, httpContext);
                ActionResult result = controller.ReportMyPackage("Mordor", "2.0.1");
                Assert.IsType<RedirectToRouteResult>(result);
                Assert.Equal("ReportAbuse", ((RedirectToRouteResult)result).RouteValues["Action"]);
            }

            [Fact]
            public async Task HtmlEncodesMessageContent()
            {
                var user = new User { Username = "Sauron", Key = 1, EmailAddress = "sauron@mordor.example.com" };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "mordor", Owners = { user } },
                    Version = "2.0.1"
                };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersionStrict("mordor", "2.0.1")).Returns(package);

                ReportPackageRequest reportRequest = null;
                var messageService = new Mock<IMessageService>();
                messageService
                    .Setup(s => s.ReportMyPackage(It.IsAny<ReportPackageRequest>()))
                    .Callback<ReportPackageRequest>(r => reportRequest = r);
                var httpContext = new Mock<HttpContextBase>();
                var controller = CreateController(
                    packageService: packageService,
                    messageService: messageService,
                    httpContext: httpContext);
                controller.SetCurrentUser(user);
                var model = new ReportMyPackageViewModel
                {
                    Message = "I like the cut of your jib. It's <b>bold</b>.",
                    Reason = ReportPackageReason.IsFraudulent
                };

                TestUtility.SetupUrlHelper(controller, httpContext);
                await controller.ReportMyPackage("mordor", "2.0.1", model);

                Assert.NotNull(reportRequest);
                Assert.Equal(user.EmailAddress, reportRequest.FromAddress.Address);
                Assert.Same(package, reportRequest.Package);
                Assert.Equal(EnumHelper.GetDescription(ReportPackageReason.IsFraudulent), reportRequest.Reason);
                Assert.Equal("I like the cut of your jib. It&#39;s &lt;b&gt;bold&lt;/b&gt;.", reportRequest.Message);
            }
        }

        public class TheUploadFileActionForGetRequests
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
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage() as ViewResult;

                Assert.NotNull(result);
            }
        }

        public class TheUploadFileActionForPostRequests
        {
            [Fact]
            public async Task WillReturn409WhenThereIsAlreadyAnUploadInProgress()
            {
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakeUploadFileService = new Mock<IUploadFileService>();
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    var controller = CreateController(
                        uploadFileService: fakeUploadFileService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.UploadPackage(null) as JsonResult;

                    Assert.NotNull(result);
                }
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfPackageFileIsNull()
            {
                var controller = CreateController();
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
                var controller = CreateController();
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
                    exceptionType.GetConstructor(new[] {typeof(string)}).Invoke(new[] { EnsureValidExceptionMessage });

                var controller = CreateController(
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

                var controller = CreateController(fakeNuGetPackage: TestPackage.CreateTestPackageStream(packageId, "1.0.0"));
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
                    packageService: fakePackageService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(String.Format(Strings.PackageIdNotAvailable, "theId"), controller.ModelState[String.Empty].Errors[0].ErrorMessage);
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
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as JsonResult;

                Assert.NotNull(result);
                Assert.True(result.Data is VerifyPackageRequest);
            }
        }

        public class TheVerifyPackageActionForPostRequests
        {
            [Fact]
            public async Task WillRedirectToUploadPageWhenThereIsNoUploadInProgress()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService);
                TestUtility.SetupUrlHelperForUrlGeneration(controller, new Uri("http://uploadpackage.xyz"));
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null }) as JsonResult;

                Assert.NotNull(result);
            }

            [Fact]
            public async Task WillCreateThePackage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageService = new Mock<IPackageService>();
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(
                        Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        packageService: fakePackageService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null });

                    fakePackageService.Verify(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), TestUtility.FakeUser, false));
                }
            }

            [Fact]
            public async Task WillSavePackageToFileStorage()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakePackageFileService = new Mock<IPackageFileService>();
                    fakePackageFileService.Setup(x => x.SavePackageFileAsync(fakePackage, It.IsAny<Stream>())).Returns(Task.FromResult(0)).Verifiable();

                    var controller = CreateController(
                        packageService: fakePackageService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        packageFileService: fakePackageFileService);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null });

                    // Assert
                    fakePackageService.Verify(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), TestUtility.FakeUser, false));
                    fakePackageFileService.Verify();
                }
            }

            [Fact]
            public async Task WillDeletePackageFileFromBlobStorageIfSavingDbChangesFails()
            {
                // Arrange
                var packageId = "theId";
                var packageVersion = "1.0.0";
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = packageId }, Version = packageVersion };
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream(packageId, packageVersion);

                    var fakePackageFileService = new Mock<IPackageFileService>();
                    fakePackageFileService.Setup(x => x.SavePackageFileAsync(fakePackage, It.IsAny<Stream>())).Returns(Task.CompletedTask).Verifiable();
                    fakePackageFileService.Setup(x => x.DeletePackageFileAsync(packageId, packageVersion)).Returns(Task.CompletedTask).Verifiable();

                    var fakeEntitiesContext = new Mock<IEntitiesContext>();
                    fakeEntitiesContext.Setup(e => e.SaveChangesAsync()).Throws<Exception>();

                    var controller = CreateController(
                        packageService: fakePackageService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        packageFileService: fakePackageFileService,
                        entitiesContext: fakeEntitiesContext);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await Assert.ThrowsAsync<Exception>(async () => await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null }));

                    // Assert
                    fakePackageService.Verify(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), TestUtility.FakeUser, false));
                    fakePackageFileService.Verify();
                }
            }

            [Fact]
            public async Task WillShowViewWithMessageIfSavingPackageBlobFails()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakePackageFileService = new Mock<IPackageFileService>();
                    fakePackageFileService.Setup(x => x.SavePackageFileAsync(fakePackage, It.IsAny<Stream>()))
                        .Throws<InvalidOperationException>();

                    var fakeEntitiesContext = new Mock<IEntitiesContext>();

                    var controller = CreateController(
                        packageService: fakePackageService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        packageFileService: fakePackageFileService,
                        entitiesContext: fakeEntitiesContext);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null });

                    // Assert
                    fakePackageService.Verify(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), TestUtility.FakeUser, false));
                    Assert.Equal(Strings.UploadPackage_IdVersionConflict, controller.TempData["Message"]);
                    fakeEntitiesContext.VerifyCommitted(Times.Never());
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
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                    var fakePackageFileService = new Mock<IPackageFileService>();
                    fakePackageFileService.Setup(x => x.SavePackageFileAsync(fakePackage, It.IsAny<Stream>())).Returns(Task.FromResult(0)).Verifiable();

                    var fakeIndexingService = new Mock<IIndexingService>(MockBehavior.Strict);
                    fakeIndexingService.Setup(f => f.UpdateIndex()).Verifiable();

                    var controller = CreateController(
                        packageService: fakePackageService,
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
            public async Task WillSaveChangesToEntitiesContext()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                using (var fakeFileStream = new MemoryStream())
                {
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key))
                        .Returns(Task.CompletedTask);
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var entitiesContext = new Mock<IEntitiesContext>();
                    entitiesContext.Setup(e => e.SaveChangesAsync())
                        .Returns(Task.FromResult(0)).Verifiable();

                    var controller = CreateController(
                        packageService: fakePackageService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage,
                        entitiesContext: entitiesContext);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null });

                    // Assert
                    entitiesContext.Verify();
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
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), false))
                        .Returns(Task.FromResult(fakePackage));
                    fakePackageService.Setup(x => x.PublishPackageAsync(fakePackage, false))
                        .Returns(Task.CompletedTask);
                    fakePackageService.Setup(x => x.MarkPackageUnlistedAsync(fakePackage, false))
                        .Returns(Task.CompletedTask);
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        packageService: fakePackageService,
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
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        packageService: fakePackageService,
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
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        packageService: fakePackageService,
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
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        packageService: fakePackageService,
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
                    var fakePackageService = new Mock<IPackageService>();
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        packageService: fakePackageService,
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
                    var fakePackageService = new Mock<IPackageService>();
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        packageService: fakePackageService,
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
                    var fakePackageService = new Mock<IPackageService>();
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var controller = CreateController(
                        packageService: fakePackageService,
                        uploadFileService: fakeUploadFileService,
                        fakeNuGetPackage: fakeNuGetPackage);
                    controller.SetCurrentUser(TestUtility.FakeUser);

                    var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null }) as JsonResult;

                    Assert.NotNull(result);
                    Assert.NotNull(result.Data);
                    Assert.Equal("{ location = /?id=theId }", result.Data.ToString());
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
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var fakeAutoCuratePackageCmd = new Mock<IAutomaticallyCuratePackageCommand>();
                    var controller = CreateController(
                        packageService: fakePackageService,
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
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");

                    var auditingService = new TestAuditingService();

                    var controller = CreateController(
                        packageService: fakePackageService,
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
                    var fakePackageService = new Mock<IPackageService>();
                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                    fakePackageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(fakePackage));
                    var fakeNuGetPackage = TestPackage.CreateTestPackageStream("theId", "1.0.0");
                    var fakeTelemetryService = new Mock<ITelemetryService>();

                    var controller = CreateController(
                        packageService: fakePackageService,
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
                    yield return new object[] { new EditPackageVersionRequest() { RequiresLicenseAcceptance = true } };
                    yield return new object[] { new EditPackageVersionRequest() { IconUrl = "https://iconnew" } };
                    yield return new object[] { new EditPackageVersionRequest() { ProjectUrl = "https://projectnew" } };
                    yield return new object[] { new EditPackageVersionRequest() { Authors = "author1new authors2new" } };
                    yield return new object[] { new EditPackageVersionRequest() { Copyright = "copyright" } };
                    yield return new object[] { new EditPackageVersionRequest() { Description = "new desc" } };
                    yield return new object[] { new EditPackageVersionRequest() { ReleaseNotes = "notes123" } };
                    yield return new object[] { new EditPackageVersionRequest() { Summary = "summary new" } };
                    yield return new object[] { new EditPackageVersionRequest() { Tags = "tag1new tag2new" } };
                    yield return new object[] { new EditPackageVersionRequest() { VersionTitle = "title" } };
                    yield return new object[] { new EditPackageVersionRequest() { ReadMeState = PackageEditReadMeState.Unchanged } };
                }
            }

            [Theory]
            [MemberData("WillApplyEdits_Data")]
            public async Task WillApplyEdits(EditPackageVersionRequest edit)
            {
                // Arrange
                using (var fakeFileStream = new MemoryStream())
                {
                    var fakeUploadFileService = new Mock<IUploadFileService>();
                    fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                    fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.CompletedTask);

                    var packageService = new Mock<IPackageService>();

                    var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "thePackageId" }, Version = "1.0.0" };
                    packageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                        .Returns(Task.FromResult(fakePackage));

                    var fakeEditPackageService = new Mock<EditPackageService>();

                    var fakePackageFileService = new Mock<IPackageFileService>();
                    fakePackageFileService.Setup(x => x.SaveReadMeFileAsync(fakePackage, It.IsAny<Stream>())).Returns(Task.CompletedTask);

                    var controller = CreateController(packageService: packageService, editPackageService: fakeEditPackageService, uploadFileService: fakeUploadFileService, packageFileService: fakePackageFileService);
                    controller.SetCurrentUser(TestUtility.FakeUser);
                
                    // Act
                    await controller.VerifyPackage(new VerifyPackageRequest { Listed = true, Edit = edit, ReadMe = new ReadMeRequest()});

                    // Assert 
                    fakeEditPackageService.Verify(x => x.StartEditPackageRequest(fakePackage, edit, TestUtility.FakeUser), Times.Once);
                    fakePackageFileService.Verify(x => x.SaveReadMeFileAsync(fakePackage, It.IsAny<Stream>()), Times.Never);
                }
            }
        }

        public static IEnumerable<object[]> WillApplyReadMe_Data
        {
            get
            {
                yield return new object[] { new EditPackageVersionRequest() { RequiresLicenseAcceptance = true } };
                yield return new object[] { new EditPackageVersionRequest() { IconUrl = "https://iconnew" } };
                yield return new object[] { new EditPackageVersionRequest() { ProjectUrl = "https://projectnew" } };
                yield return new object[] { new EditPackageVersionRequest() { Authors = "author1new authors2new" } };
                yield return new object[] { new EditPackageVersionRequest() { Copyright = "copyright" } };
                yield return new object[] { new EditPackageVersionRequest() { Description = "new desc" } };
                yield return new object[] { new EditPackageVersionRequest() { ReleaseNotes = "notes123" } };
                yield return new object[] { new EditPackageVersionRequest() { Summary = "summary new" } };
                yield return new object[] { new EditPackageVersionRequest() { Tags = "tag1new tag2new" } };
                yield return new object[] { new EditPackageVersionRequest() { VersionTitle = "title" } };
                yield return new object[] { new EditPackageVersionRequest() { ReadMeState = PackageEditReadMeState.Unchanged } };
            }
        }

        [Theory]
        [MemberData("WillApplyReadMe_Data")]
        public async Task WillApplyReadMeForWrittenReadMeData(EditPackageVersionRequest edit)
        {
            // Arrange
            using (var fakeFileStream = new MemoryStream())
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.CompletedTask);

                var packageService = new Mock<IPackageService>();

                var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "thePackageId" }, Version = "1.0.0" };
                packageService.Setup(x => x.CreatePackageAsync(It.IsAny<PackageArchiveReader>(), It.IsAny<PackageStreamMetadata>(), It.IsAny<User>(), It.IsAny<bool>()))
                    .Returns(Task.FromResult(fakePackage));

                var fakeEditPackageService = new Mock<EditPackageService>();

                var fakePackageFileService = new Mock<IPackageFileService>();
                fakePackageFileService.Setup(x => x.SaveReadMeFileAsync(fakePackage, It.IsAny<Stream>())).Returns(Task.CompletedTask);
                
                var controller = CreateController(packageService: packageService, editPackageService: fakeEditPackageService, uploadFileService: fakeUploadFileService, packageFileService: fakePackageFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var fakeReadMeRequest = new Mock<ReadMeRequest>();
                fakeReadMeRequest.Setup(x => x.ReadMeWritten).Returns("fakeReadMeStream");
                fakeReadMeRequest.Setup(x => x.ReadMeType).Returns("Written");
                
                var fakeVerifyPackageRequest = new VerifyPackageRequest { Listed = true, Edit = edit, ReadMe = fakeReadMeRequest.Object };
                
                // Act
                await controller.VerifyPackage(fakeVerifyPackageRequest);

                // Assert
                Assert.Equal(PackageEditReadMeState.Changed, fakeVerifyPackageRequest.ReadMe.ReadMeState);
                fakePackageFileService.Verify(x => x.SaveReadMeFileAsync(fakePackage, It.IsAny<Stream>()), Times.Once);
                fakeEditPackageService.Verify(x => x.StartEditPackageRequest(fakePackage, edit, TestUtility.FakeUser), Times.Once);
            }
        }

    public class TheUploadProgressAction
        {
            private static readonly string FakeUploadName = "upload-" + TestUtility.FakeUserName;

            [Fact]
            public void WillReturnHttpNotFoundForUnknownUser()
            {
                // Arrange
                var cacheService = new Mock<ICacheService>(MockBehavior.Strict);
                cacheService.Setup(c => c.GetItem(FakeUploadName)).Returns(null);

                var controller = CreateController(cacheService: cacheService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                // Act
                var result = controller.UploadPackageProgress();

                // Assert
                Assert.IsType<JsonResult>(result);
            }

            [Fact]
            public void WillReturnCorrectResultForKnownUser()
            {
                var cacheService = new Mock<ICacheService>(MockBehavior.Strict);
                cacheService.Setup(c => c.GetItem(FakeUploadName))
                            .Returns(new AsyncFileUploadProgress(100) { FileName = "haha", TotalBytesRead = 80 });

                var controller = CreateController(cacheService: cacheService);
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

                var controller = CreateController(packageService: packageService, httpContext: httpContext, indexingService: indexingService);
                controller.SetCurrentUser(new User("Smeagol"));
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = await controller.SetLicenseReportVisibility("Foo", "1.0", visible: false, urlFactory: p => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                indexingService.Verify(i => i.UpdatePackage(package));
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
            }
        }

        private static async Task<ActionResult> GetDisplayPackageResultWithReadMeStream(Stream readMeHtmlStream, bool hasReadMe)
        {
            var packageService = new Mock<IPackageService>();
            var indexingService = new Mock<IIndexingService>();
            var fileService = new Mock<IPackageFileService>();
            var controller = CreateController(
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
                fileService.Setup(f => f.DownloadReadmeFileAsync(It.IsAny<Package>())).Returns(Task.FromResult(readMeHtmlStream));
            }

            return await controller.DisplayPackage("Foo", /*version*/null);
        }
    }
}

