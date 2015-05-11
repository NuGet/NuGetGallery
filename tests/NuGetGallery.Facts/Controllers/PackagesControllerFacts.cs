// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Moq;
using NuGet;
using NuGetGallery.AsyncFileUpload;
using NuGetGallery.Configuration;
using NuGetGallery.Packaging;
using NuGetGallery.Helpers;
using Xunit;
using Xunit.Extensions;
using System.Collections.Generic;
using NuGetGallery.Framework;

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
            Mock<INupkg> fakeNuGetPackage = null,
            Mock<ISearchService> searchService = null,
            Exception readPackageException = null,
            Mock<IAutomaticallyCuratePackageCommand> autoCuratePackageCmd = null,
            Mock<IAppConfiguration> config = null,
            Mock<IPackageFileService> packageFileService = null,
            Mock<IEntitiesContext> entitiesContext = null,
            Mock<IIndexingService> indexingService = null,
            Mock<ICacheService> cacheService = null)
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

            if (packageFileService == null)
            {
                packageFileService = new Mock<IPackageFileService>();
                packageFileService.Setup(p => p.SavePackageFileAsync(It.IsAny<Package>(), It.IsAny<Stream>())).Returns(Task.FromResult(0));
            }

            entitiesContext = entitiesContext ?? new Mock<IEntitiesContext>();

            indexingService = indexingService ?? new Mock<IIndexingService>();

            cacheService = cacheService ?? new Mock<ICacheService>();

            editPackageService = editPackageService ?? new Mock<EditPackageService>();

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
                editPackageService.Object);
            controller.CallBase = true;
            controller.Object.OwinContext = Fakes.CreateOwinContext();

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
                    fakeNuGetPackage = new Mock<INupkg>();
                    fakeNuGetPackage.Setup(p => p.Metadata.Id).Returns("thePackageId");
                }

                controller.Setup(x => x.CreatePackage(It.IsAny<Stream>())).Returns(fakeNuGetPackage.Object);
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

                var result = await controller.CancelUpload() as RedirectToRouteResult;

                Assert.False(result.Permanent);
                Assert.Equal("UploadPackage", result.RouteValues["Action"]);
            }
        }

        public class TheDisplayPackageMethod
        {
            [Fact]
            public void GivenANonNormalizedVersionIt302sToTheNormalizedVersion()
            {
                // Arrange
                var controller = CreateController();

                // Act
                var result = controller.DisplayPackage("Foo", "01.01.01");

                // Assert
                ResultAssert.IsRedirectToRoute(result, new
                {
                    action = "DisplayPackage",
                    id = "Foo",
                    version = "1.1.1"
                }, permanent: true);
            }

            [Fact]
            public void GivenANonExistantPackageIt404s()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var controller = CreateController(packageService: packageService);

                packageService.Setup(p => p.FindPackageByIdAndVersion("Foo", "1.1.1", true))
                              .ReturnsNull();

                // Act
                var result = controller.DisplayPackage("Foo", "1.1.1");

                // Assert
                ResultAssert.IsNotFound(result);
            }

            [Fact]
            public void GivenAValidPackageThatTheCurrentUserDoesNotOwnItDisplaysCurrentMetadata()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var controller = CreateController(
                    packageService: packageService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                packageService.Setup(p => p.FindPackageByIdAndVersion("Foo", "1.1.1", true))
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

                // Act
                var result = controller.DisplayPackage("Foo", "1.1.1");

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("1.1.1", model.Version);
                Assert.Equal("A test package!", model.Title);
            }

            [Fact]
            public void GivenAValidPackageThatTheCurrentUserOwnsItDisablesResponseCaching()
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
                    .Setup(p => p.FindPackageByIdAndVersion("Foo", "1.1.1", true))
                    .Returns(package);
                
                // Act
                controller.DisplayPackage("Foo", "1.1.1");

                // Assert
                httpCachePolicy.VerifyAll();
            }

            [Fact]
            public void GivenAValidPackageThatTheCurrentUserOwnsWithNoEditsItDisplaysCurrentMetadata()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var editPackageService = new Mock<EditPackageService>();
                var httpContext = new Mock<HttpContextBase>();
                var httpCachePolicy = new Mock<HttpCachePolicyBase>();
                var controller = CreateController(
                    packageService: packageService,
                    editPackageService: editPackageService,
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
                    .Setup(p => p.FindPackageByIdAndVersion("Foo", "1.1.1", true))
                    .Returns(package);
                editPackageService
                    .Setup(e => e.GetPendingMetadata(package))
                    .ReturnsNull();

                // Act
                var result = controller.DisplayPackage("Foo", "1.1.1");

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("1.1.1", model.Version);
                Assert.Equal("A test package!", model.Title);
            }

            [Fact]
            public void GivenAValidPackageThatTheCurrentUserOwnsWithEditsItDisplaysEditedMetadata()
            {
                // Arrange
                var packageService = new Mock<IPackageService>();
                var editPackageService = new Mock<EditPackageService>();
                var httpContext = new Mock<HttpContextBase>();
                var httpCachePolicy = new Mock<HttpCachePolicyBase>();
                var controller = CreateController(
                    packageService: packageService,
                    editPackageService: editPackageService,
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
                    .Setup(p => p.FindPackageByIdAndVersion("Foo", "1.1.1", true))
                    .Returns(package);
                editPackageService
                    .Setup(e => e.GetPendingMetadata(package))
                    .Returns(new PackageEdit()
                    {
                        Title = "A modified package!"
                    });

                // Act
                var result = controller.DisplayPackage("Foo", "1.1.1");

                // Assert
                var model = ResultAssert.IsView<DisplayPackageViewModel>(result);
                Assert.Equal("Foo", model.Id);
                Assert.Equal("1.1.1", model.Version);
                Assert.Equal("A modified package!", model.Title);
            }
        }

        public class TheConfirmOwnerMethod
        {
            [Fact]
            public void WithEmptyTokenReturnsHttpNotFound()
            {
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(new PackageRegistration());
                var controller = CreateController(packageService: packageService);
                controller.SetCurrentUser(new User { Username = "username" });
                
                var result = controller.ConfirmOwner("foo", "username", "");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WithNonExistentPackageIdReturnsHttpNotFound()
            {
                // Arrange
                var controller = CreateController();
                controller.SetCurrentUser(new User { Username = "username" });
                
                // Act
                var result = controller.ConfirmOwner("foo", "username", "token");
                
                // Assert
                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WithIdentityNotMatchingUserInRequestReturnsViewWithMessage()
            {
                var controller = CreateController();
                controller.SetCurrentUser("userA");
                var result = controller.ConfirmOwner("foo", "userB", "token");
                
                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result);
                Assert.Equal(ConfirmOwnershipResult.NotYourRequest, model.Result);
                Assert.Equal("userB", model.Username);
            }

            [Theory]
            [InlineData(ConfirmOwnershipResult.Success)]
            [InlineData(ConfirmOwnershipResult.AlreadyOwner)]
            [InlineData(ConfirmOwnershipResult.Failure)]
            public void AcceptsResultOfPackageServiceIfOtherwiseValid(ConfirmOwnershipResult confirmationResult)
            {
                var package = new PackageRegistration { Id = "foo" };
                var user = new User { Username = "username" };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("foo")).Returns(package);
                packageService.Setup(p => p.ConfirmPackageOwner(package, user, "token")).Returns(confirmationResult);
                var controller = CreateController(packageService: packageService);
                controller.SetCurrentUser(user);
                
                var result = controller.ConfirmOwner("foo", "username", "token");

                var model = ResultAssert.IsView<PackageOwnerConfirmationModel>(result);
                Assert.Equal(confirmationResult, model.Result);
                Assert.Equal("foo", model.PackageId);
            }
        }

        public class TheContactOwnersMethod
        {
            [Fact]
            public void OnlyShowsOwnersWhoAllowReceivingEmails()
            {
                var package = new PackageRegistration
                    {
                        Id = "pkgid",
                        Owners = new[]
                            {
                                new User { Username = "helpful", EmailAllowed = true },
                                new User { Username = "grinch", EmailAllowed = false },
                                new User { Username = "helpful2", EmailAllowed = true }
                            }
                    };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageRegistrationById("pkgid")).Returns(package);
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
            public void UpdatesUnlistedIfSelected()
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
                packageService.Setup(svc => svc.MarkPackageListed(It.IsAny<Package>(), It.IsAny<bool>())).Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.MarkPackageUnlisted(It.IsAny<Package>(), It.IsAny<bool>())).Verifiable();
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0", true)).Returns(package).Verifiable();

                var indexingService = new Mock<IIndexingService>();

                var controller = CreateController(packageService: packageService, indexingService: indexingService);
                controller.SetCurrentUser("Frodo");
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = controller.Edit("Foo", "1.0", listed: false, urlFactory: p => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                indexingService.Verify(i => i.UpdatePackage(package));
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
            }

            [Fact]
            public void UpdatesUnlistedIfNotSelected()
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
                packageService.Setup(svc => svc.MarkPackageListed(It.IsAny<Package>(), It.IsAny<bool>())).Verifiable();
                packageService.Setup(svc => svc.MarkPackageUnlisted(It.IsAny<Package>(), It.IsAny<bool>())).Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0", true)).Returns(package).Verifiable();

                var indexingService = new Mock<IIndexingService>();

                var controller = CreateController(packageService: packageService, indexingService: indexingService);
                controller.SetCurrentUser("Frodo");
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = controller.Edit("Foo", "1.0", listed: true, urlFactory: p => @"~\Bar.cshtml");

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

                var result = (await controller.ListPackages(" test ")) as ViewResult;

                var model = result.Model as PackageListViewModel;
                Assert.Equal("test", model.SearchTerm);
            }
        }

        public class TheReportAbuseMethod
        {
            [Fact]
            public void SendsMessageToGalleryOwnerWithEmailOnlyWhenUnauthenticated()
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
                packageService.Setup(p => p.FindPackageByIdAndVersion("mordor", "2.0.1", true)).Returns(package);
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
                var result = controller.ReportAbuse("mordor", "2.0.1", model) as RedirectResult;

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
            public void SendsMessageToGalleryOwnerWithUserInfoWhenAuthenticated()
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
                packageService.Setup(p => p.FindPackageByIdAndVersion("mordor", It.IsAny<string>(), true)).Returns(package);
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
                ActionResult result = controller.ReportAbuse("mordor", "2.0.1", model) as RedirectResult;

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
                packageService.Setup(p => p.FindPackageByIdAndVersion("Mordor", It.IsAny<string>(), true)).Returns(package);
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
            public void HtmlEncodesMessageContent()
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
                packageService.Setup(p => p.FindPackageByIdAndVersion("mordor", "2.0.1", true)).Returns(package);
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
                controller.ReportAbuse("mordor", "2.0.1", model);

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
                packageService.Setup(p => p.FindPackageByIdAndVersion("Mordor", It.IsAny<string>(), true)).Returns(package);
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
            public void HtmlEncodesMessageContent()
            {
                var user = new User { Username = "Sauron", Key = 1, EmailAddress = "sauron@mordor.example.com" };
                var package = new Package
                {
                    PackageRegistration = new PackageRegistration { Id = "mordor", Owners = { user } },
                    Version = "2.0.1"
                };
                var packageService = new Mock<IPackageService>();
                packageService.Setup(p => p.FindPackageByIdAndVersion("mordor", "2.0.1", true)).Returns(package);
                
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
                var model = new ReportAbuseViewModel
                {
                    Email = "frodo@hobbiton.example.com",
                    Message = "I like the cut of your jib. It's <b>bold</b>.",
                    Reason = ReportPackageReason.IsFraudulent,
                    AlreadyContactedOwner = true,
                };

                TestUtility.SetupUrlHelper(controller, httpContext);
                controller.ReportMyPackage("mordor", "2.0.1", model);

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
                var fakeFileStream = new MemoryStream();
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage() as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.VerifyPackage, result.RouteName);
                fakeFileStream.Dispose();
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
                var fakeFileStream = new MemoryStream();
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(null) as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(409, result.StatusCode);
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfPackageFileIsNull()
            {
                var controller = CreateController();
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(null) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UploadFileIsRequired, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfFileIsNotANuGetPackage()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.notNuPkg");
                var controller = CreateController();
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.UploadFileMustBeNuGetPackage, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillShowViewWithErrorsIfNuGetPackageIsInvalid()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var readPackageException = new Exception();

                var controller = CreateController(
                    readPackageException: readPackageException);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(Strings.FailedToReadUploadFile, controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillShowTheViewWithErrorsWhenThePackageIdIsAlreadyBeingUsed()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakePackageRegistration = new PackageRegistration { Id = "theId", Owners = new[] { new User { Key = 1 /* not the current user */ } } };
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageRegistrationById(It.IsAny<string>())).Returns(fakePackageRegistration);
                var controller = CreateController(
                    packageService: fakePackageService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(String.Format(Strings.PackageIdNotAvailable, "theId"), controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillShowTheViewWithErrorsWhenThePackageAlreadyExists()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.FindPackageByIdAndVersion(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var controller = CreateController(
                    packageService: fakePackageService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as ViewResult;

                Assert.NotNull(result);
                Assert.False(controller.ModelState.IsValid);
                Assert.Equal(
                    String.Format(Strings.PackageExistsAndCannotBeModified, "theId", "theVersion"),
                    controller.ModelState[String.Empty].Errors[0].ErrorMessage);
            }

            [Fact]
            public async Task WillSaveTheUploadFile()
            {
                var fakeUploadedFile = new Mock<HttpPostedFileBase>();
                fakeUploadedFile.Setup(x => x.FileName).Returns("theFile.nupkg");
                var fakeFileStream = new MemoryStream();
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakeNuGetPackage = new Mock<INupkg>();
                fakeNuGetPackage.Setup(p => p.Metadata.Id).Returns("thePackageId");
                fakeNuGetPackage.Setup(x => x.GetStream()).Returns(fakeFileStream);
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
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
                var fakeFileStream = new MemoryStream();
                fakeUploadedFile.Setup(x => x.InputStream).Returns(fakeFileStream);
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.UploadPackage(fakeUploadedFile.Object) as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.VerifyPackage, result.RouteName);
            }
        }

        public class TheVerifyPackageActionForGetRequests
        {
            [Fact]
            public async Task WillRedirectToUploadPackagePageWhenThereIsNoUploadInProgress()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns<Stream>(null);
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(null));
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.VerifyPackage() as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.UploadPackage, result.RouteName);
            }

            [Fact]
            public async Task WillPassThePackageIdToTheView()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<INupkg>();
                fakeNuGetPackage.Setup(x => x.Metadata.Id).Returns("theId");
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageRequest;

                Assert.Equal("theId", model.Id);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageVersionToTheView()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<INupkg>();
                fakeNuGetPackage.Setup(x => x.Metadata.Version).Returns(new SemanticVersion("1.0.42"));
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageRequest;

                Assert.Equal("1.0.42", model.Version);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageTitleToTheView()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<INupkg>();
                fakeNuGetPackage.Setup(x => x.Metadata.Title).Returns("theTitle");
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageRequest;

                Assert.Equal("theTitle", model.Edit.VersionTitle);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageSummaryToTheView()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<INupkg>();
                fakeNuGetPackage.Setup(x => x.Metadata.Summary).Returns("theSummary");
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageRequest;

                Assert.Equal("theSummary", model.Edit.Summary);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageDescriptionToTheView()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<INupkg>();
                fakeNuGetPackage.Setup(x => x.Metadata.Description).Returns("theDescription");
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageRequest;

                Assert.Equal("theDescription", model.Edit.Description);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageLicenseAcceptanceRequirementToTheView()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<INupkg>();
                fakeNuGetPackage.Setup(x => x.Metadata.RequireLicenseAcceptance).Returns(true);
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageRequest;

                Assert.True(model.Edit.RequiresLicenseAcceptance);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageLicenseUrlToTheView()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<INupkg>();
                fakeNuGetPackage.Setup(x => x.Metadata.LicenseUrl).Returns(new Uri("http://theLicenseUri"));
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageRequest;

                Assert.Equal("http://thelicenseuri/", model.LicenseUrl);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageTagsToTheView()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<INupkg>();
                fakeNuGetPackage.Setup(x => x.Metadata.Tags).Returns("theTags");
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageRequest;

                Assert.Equal("theTags", model.Edit.Tags);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackageProjectUrlToTheView()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<INupkg>();
                fakeNuGetPackage.Setup(x => x.Metadata.ProjectUrl).Returns(new Uri("http://theProjectUri"));
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageRequest;

                Assert.Equal("http://theprojecturi/", model.Edit.ProjectUrl);
                fakeUploadFileStream.Dispose();
            }

            [Fact]
            public async Task WillPassThePackagAuthorsToTheView()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeUploadFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeUploadFileStream));
                var fakeNuGetPackage = new Mock<INupkg>();
                fakeNuGetPackage.Setup(x => x.Metadata.Authors).Returns(new[] { "firstAuthor", "secondAuthor" });
                var controller = CreateController(
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var model = ((ViewResult)await controller.VerifyPackage()).Model as VerifyPackageRequest;

                Assert.Equal("firstAuthor, secondAuthor", model.Edit.Authors);
                fakeUploadFileStream.Dispose();
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

                var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null }) as RedirectResult;

                Assert.NotNull(result);
            }

            [Fact]
            public async Task WillCreateThePackage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var fakeNuGetPackage = new Mock<INupkg>();

                var controller = CreateController(
                    packageService: fakePackageService,
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null });

                fakePackageService.Verify(x => x.CreatePackage(fakeNuGetPackage.Object, TestUtility.FakeUser, false));
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillSavePackageToFileStorage()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(fakePackage);
                var fakeNuGetPackage = new Mock<INupkg>();
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
                fakePackageService.Verify(x => x.CreatePackage(fakeNuGetPackage.Object, TestUtility.FakeUser, false));
                fakePackageFileService.Verify();
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillUpdateIndexingService()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(fakePackage);
                var fakeNuGetPackage = new Mock<INupkg>();
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
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillSaveChangesToEntitiesContext()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(fakePackage);
                var fakeNuGetPackage = new Mock<INupkg>();

                var entitiesContext = new Mock<IEntitiesContext>();
                entitiesContext.Setup(e => e.SaveChanges()).Verifiable();

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
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillNotCommitChangesToPackageService()
            {
                // Arrange
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>(MockBehavior.Strict);
                var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), false)).Returns(fakePackage);
                fakePackageService.Setup(x => x.PublishPackage(fakePackage, false));
                fakePackageService.Setup(x => x.MarkPackageUnlisted(fakePackage, false));
                var fakeNuGetPackage = new Mock<INupkg>();

                var controller = CreateController(
                    packageService: fakePackageService,
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                // Act
                await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null });

                // There's no assert. If the method completes, it means the test pass because we set MockBehavior to Strict
                // for the fakePackageService. We verified that it only calls methods passing commitSettings = false.

                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillPublishThePackage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(fakePackage);
                var fakeNuGetPackage = new Mock<INupkg>();
                var controller = CreateController(
                    packageService: fakePackageService,
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.VerifyPackage(new VerifyPackageRequest() { Listed = true, Edit = null });

                fakePackageService.Verify(x => x.PublishPackage(fakePackage, false), Times.Once());
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillMarkThePackageUnlistedWhenListedArgumentIsFalse()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var fakeNuGetPackage = new Mock<INupkg>();
                var controller = CreateController(
                    packageService: fakePackageService,
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null });

                fakePackageService.Verify(
                    x => x.MarkPackageUnlisted(It.Is<Package>(p => p.PackageRegistration.Id == "theId" && p.Version == "theVersion"), It.IsAny<bool>()));
                fakeFileStream.Dispose();
            }

            [Theory]
            [InlineData(new object[] { null })]
            [InlineData(new object[] { true })]
            public async Task WillNotMarkThePackageUnlistedWhenListedArgumentIsNullorTrue(bool? listed)
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var fakeNuGetPackage = new Mock<INupkg>();
                var controller = CreateController(
                    packageService: fakePackageService,
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.VerifyPackage(new VerifyPackageRequest() { Listed = listed.GetValueOrDefault(true), Edit = null });

                fakePackageService.Verify(x => x.MarkPackageUnlisted(It.IsAny<Package>(), It.IsAny<bool>()), Times.Never());
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillDeleteTheUploadFile()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0)).Verifiable();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(
                    new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" });
                var fakeNuGetPackage = new Mock<INupkg>();
                var controller = CreateController(
                    packageService: fakePackageService,
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null });

                fakeUploadFileService.Verify();
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillSetAFlashMessage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.SaveUploadFileAsync(TestUtility.FakeUser.Key, It.IsAny<Stream>())).Returns(Task.FromResult(0));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(
                    (new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                var fakeNuGetPackage = new Mock<INupkg>();
                var controller = CreateController(
                    packageService: fakePackageService,
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null });

                Assert.Equal(String.Format(Strings.SuccessfullyUploadedPackage, "theId", "theVersion"), controller.TempData["Message"]);
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillRedirectToPackagePage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(
                    (new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" }));
                var fakeNuGetPackage = new Mock<INupkg>();
                var controller = CreateController(
                    packageService: fakePackageService,
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage);
                controller.SetCurrentUser(TestUtility.FakeUser);

                var result = await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null }) as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.DisplayPackage, result.RouteName);
                fakeFileStream.Dispose();
            }

            [Fact]
            public async Task WillCurateThePackage()
            {
                var fakeUploadFileService = new Mock<IUploadFileService>();
                var fakeFileStream = new MemoryStream();
                fakeUploadFileService.Setup(x => x.GetUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult<Stream>(fakeFileStream));
                fakeUploadFileService.Setup(x => x.DeleteUploadFileAsync(TestUtility.FakeUser.Key)).Returns(Task.FromResult(0));
                var fakePackageService = new Mock<IPackageService>();
                var fakePackage = new Package { PackageRegistration = new PackageRegistration { Id = "theId" }, Version = "theVersion" };
                fakePackageService.Setup(x => x.CreatePackage(It.IsAny<INupkg>(), It.IsAny<User>(), It.IsAny<bool>())).Returns(fakePackage);
                var fakeNuGetPackage = new Mock<INupkg>();
                var fakeAutoCuratePackageCmd = new Mock<IAutomaticallyCuratePackageCommand>();
                var controller = CreateController(
                    packageService: fakePackageService,
                    uploadFileService: fakeUploadFileService,
                    fakeNuGetPackage: fakeNuGetPackage,
                    autoCuratePackageCmd: fakeAutoCuratePackageCmd);
                controller.SetCurrentUser(TestUtility.FakeUser);

                await controller.VerifyPackage(new VerifyPackageRequest() { Listed = false, Edit = null });

                fakeAutoCuratePackageCmd.Verify(fake => fake.Execute(fakePackage, fakeNuGetPackage.Object, false));
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
                Assert.True(result is HttpNotFoundResult);
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
            public void IndexingAndPackageServicesAreUpdated()
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
                packageService.Setup(svc => svc.SetLicenseReportVisibility(It.IsAny<Package>(), It.Is<bool>(t => t == true), It.IsAny<bool>())).Throws(new Exception("Shouldn't be called"));
                packageService.Setup(svc => svc.SetLicenseReportVisibility(It.IsAny<Package>(), It.Is<bool>(t => t == false), It.IsAny<bool>())).Verifiable();
                packageService.Setup(svc => svc.FindPackageByIdAndVersion("Foo", "1.0", true)).Returns(package).Verifiable();

                var httpContext = new Mock<HttpContextBase>();
                httpContext.Setup(h => h.Request.IsAuthenticated).Returns(true);
                httpContext.Setup(h => h.User.Identity.Name).Returns("Smeagol");

                var indexingService = new Mock<IIndexingService>();

                var controller = CreateController(packageService: packageService, httpContext: httpContext, indexingService: indexingService);
                controller.SetCurrentUser("Smeagol");
                controller.Url = new UrlHelper(new RequestContext(), new RouteCollection());

                // Act
                var result = controller.SetLicenseReportVisibility("Foo", "1.0", visible: false, urlFactory: p => @"~\Bar.cshtml");

                // Assert
                packageService.Verify();
                indexingService.Verify(i => i.UpdatePackage(package));
                Assert.IsType<RedirectResult>(result);
                Assert.Equal(@"~\Bar.cshtml", ((RedirectResult)result).Url);
            }
        }
    }
}

