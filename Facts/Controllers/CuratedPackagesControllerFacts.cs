using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Principal;
using System.Web.Mvc;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class CuratedPackagesControllerFacts
    {
        public class TestableCuratedPackagesController : CuratedPackagesController
        {
            public TestableCuratedPackagesController()
            {
                StubCuratedFeed = new CuratedFeed
                    { Key = 0, Name = "aFeedName", Managers = new HashSet<User>(new[] { new User { Username = "aUsername" } }) };
                StubIdentity = new Mock<IIdentity>();
                StubPackageRegistration = new PackageRegistration { Key = 0, Id = "anId" };

                StubIdentity.Setup(stub => stub.IsAuthenticated).Returns(true);
                StubIdentity.Setup(stub => stub.Name).Returns("aUsername");

                EntitiesContext = new FakeEntitiesContext();
                EntitiesContext.CuratedFeeds.Add(StubCuratedFeed);
                EntitiesContext.PackageRegistrations.Add(StubPackageRegistration);

                var curatedFeedRepository = new EntityRepository<CuratedFeed>(
                    EntitiesContext);

                var curatedPackageRepository = new EntityRepository<CuratedPackage>(
                    EntitiesContext);

                base.CuratedFeedService = new CuratedFeedService(
                    curatedFeedRepository,
                    curatedPackageRepository);
            }

            public CuratedFeed StubCuratedFeed { get; set; }
            public Mock<IIdentity> StubIdentity { get; private set; }
            public PackageRegistration StubPackageRegistration { get; private set; }

            protected override IIdentity Identity
            {
                get { return StubIdentity.Object; }
            }
        }

        public class TheDeleteCuratedPackageAction
        {
            [Fact]
            public void WillReturn404IfTheCuratedFeedDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();
                
                var result = controller.DeleteCuratedPackage("aStrangeCuratedFeedName", "anId");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WillReturn404IfTheCuratedPackageDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();
                controller.StubCuratedFeed.Packages = new[] { new CuratedPackage { PackageRegistration = new PackageRegistration() } };

                var result = controller.DeleteCuratedPackage("aFeedName", "aStrangeCuratedPackageId");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WillReturn403IfTheUserNotAManager()
            {
                var controller = new TestableCuratedPackagesController();
                controller.StubIdentity
                    .Setup(i => i.Name)
                    .Returns("notAManager");

                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                    });

                var result = controller.DeleteCuratedPackage("aFeedName", "anId") as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(403, result.StatusCode);
            }

            [Fact]
            public void WillDeleteTheCuratedPackageWhenRequestIsValid()
            {
                var controller = new TestableCuratedPackagesController();

                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration ,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                    });

                Assert.True(controller.EntitiesContext.CuratedPackages.Any
                    (cp => cp.PackageRegistration.Id == "anId"));

                controller.DeleteCuratedPackage("aFeedName", "anId");

                Assert.False(controller.EntitiesContext.CuratedPackages.Any
                    (cp => cp.PackageRegistration.Id == "anId"));
            }

            [Fact]
            public void WillReturn204AfterDeletingTheCuratedPackage()
            {
                var controller = new TestableCuratedPackagesController();

                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                    });

                var result = controller.DeleteCuratedPackage("aFeedName", "anId") as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(204, result.StatusCode);
            }
        }

        public class TheGetCreateCuratedPackageFormAction
        {
            [Fact]
            public void WillReturn404IfTheCuratedFeedDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();

                var result = controller.GetCreateCuratedPackageForm("aWrongFeedName");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WillReturn403IfTheCurrentUsersIsNotAManagerOfTheCuratedFeed()
            {
                var controller = new TestableCuratedPackagesController();
                controller.StubIdentity.Setup(stub => stub.Name).Returns("notAManager");

                var result = controller.GetCreateCuratedPackageForm("aFeedName") as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(403, result.StatusCode);
            }

            [Fact]
            public void WillPushTheCuratedFeedNameIntoTheViewBag()
            {
                var controller = new TestableCuratedPackagesController();
                controller.StubCuratedFeed.Name = "theCuratedFeedName";

                var result = controller.GetCreateCuratedPackageForm("theCuratedFeedName") as ViewResult;

                Assert.NotNull(result);
                Assert.Equal("theCuratedFeedName", result.ViewBag.CuratedFeedName);
            }
        }

        public class ThePatchCuratedPackageAction
        {
            [Fact]
            public void WillReturn404IfTheCuratedFeedDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();
                
                var result = controller.PatchCuratedPackage("aWrongFeedName", "anId", 
                    new ModifyCuratedPackageRequest());

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WillReturn404IfTheCuratedPackageDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();

                var result = controller.PatchCuratedPackage("aFeedName", "aWrongId", new ModifyCuratedPackageRequest());

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WillReturn403IfNotAFeedManager()
            {
                var controller = new TestableCuratedPackagesController();
                controller.StubIdentity
                    .Setup(i => i.Name)
                    .Returns("notAManager");
                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                    });

                var result = controller.PatchCuratedPackage("aFeedName", "anId", 
                        new ModifyCuratedPackageRequest()) as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(403, result.StatusCode);
            }

            [Fact]
            public void WillReturn400IfTheModelStateIsInvalid()
            {
                var controller = new TestableCuratedPackagesController();
                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                    });
                controller.ModelState.AddModelError("", "anError");

                var result = controller.PatchCuratedPackage(
                    "aFeedName", 
                    "anId", 
                    new ModifyCuratedPackageRequest()) as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(400, result.StatusCode);
            }

            [Fact]
            public void WillModifyTheCuratedPackageWhenRequestIsValid()
            {
                var controller = new TestableCuratedPackagesController();
                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                        Included = true,
                    });

                Assert.False(controller.StubCuratedFeed.Packages.Any(
                    cp => cp.Included == false));

                var result = controller.PatchCuratedPackage(
                    "aFeedName",
                    "anId",
                    new ModifyCuratedPackageRequest { Included = false }) as HttpStatusCodeResult;

                Assert.True(controller.StubCuratedFeed.Packages.Any(
                    cp => cp.Included == false));
            }

            [Fact]
            public void WillReturn204AfterModifyingTheCuratedPackage()
            {
                var controller = new TestableCuratedPackagesController();
                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                        Included = true,
                    });

                var result = controller.PatchCuratedPackage("aFeedName", "anId", 
                    new ModifyCuratedPackageRequest()) as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(204, result.StatusCode);
            }
        }

        public class ThePostCuratedPackagesAction
        {
            [Fact]
            public void WillReturn404IfTheCuratedFeedDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();

                var result = controller.PostCuratedPackages(
                    "aWrongFeedName", 
                    new CreateCuratedPackageRequest { PackageId = "AnId" });

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WillReturn403IfTheCurrentUsersIsNotAManagerOfTheCuratedFeed()
            {
                var controller = new TestableCuratedPackagesController();
                controller.StubIdentity.Setup(stub => stub.Name).Returns("notAManager");

                var result = controller.PostCuratedPackages(
                    "aFeedName",
                    new CreateCuratedPackageRequest { PackageId = "AnId" })
                    as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(403, result.StatusCode);
            }

            [Fact]
            public void WillPushTheCuratedFeedNameIntoTheViewBagAndShowTheCreateCuratedPackageFormWithErrorsWhenModelStateIsInvalid()
            {
                var controller = new TestableCuratedPackagesController();
                controller.StubCuratedFeed.Name = "theCuratedFeedName";
                controller.ModelState.AddModelError("", "anError");

                var result = controller.PostCuratedPackages(
                    "theCuratedFeedName", new CreateCuratedPackageRequest()) as ViewResult;

                Assert.NotNull(result);
                Assert.Equal("theCuratedFeedName", result.ViewBag.CuratedFeedName);
                Assert.Equal("CreateCuratedPackageForm", result.ViewName);
            }

            [Fact]
            public void WillPushTheCuratedFeedNameIntoTheViewBagAndShowTheCreateCuratedPackageFormWithErrorsWhenThePackageIdDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();

                var result = controller.PostCuratedPackages("aFeedName",
                    new CreateCuratedPackageRequest { PackageId = "aWrongId" }) as ViewResult;

                Assert.NotNull(result);
                Assert.Equal("aFeedName", result.ViewBag.CuratedFeedName);
                Assert.Equal(Strings.PackageWithIdDoesNotExist, controller.ModelState["PackageId"].Errors[0].ErrorMessage);
                Assert.Equal("CreateCuratedPackageForm", result.ViewName);
            }

            [Fact]
            public void WillCreateTheCuratedPackage()
            {
                var controller = new TestableCuratedPackagesController();

                controller.PostCuratedPackages(
                    "aFeedName",
                    new CreateCuratedPackageRequest
                        {
                            PackageId = "anId",
                            Notes = "theNotes"
                        });

                Assert.True(controller.EntitiesContext.Set<CuratedPackage>()
                    .Any(cp => cp.PackageRegistration.Id == "anId"));

                Assert.True(controller.EntitiesContext.Set<CuratedPackage>()
                    .Any(cp => cp.Notes == "theNotes"));
            }

            [Fact]
            public void WillRedirectToTheCuratedFeedRouteAfterCreatingTheCuratedPackage()
            {
                var controller = new TestableCuratedPackagesController();

                var result = controller.PostCuratedPackages(
                    "aFeedName", new CreateCuratedPackageRequest { PackageId = "anId" }) 
                    as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.CuratedFeed, result.RouteName);
            }

            [Fact]
            public void WillShowAnErrorWhenThePackageHasAlreadyBeenCurated()
            {
                var controller = new TestableCuratedPackagesController();
                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage { 
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key
                    });

                var result = controller.PostCuratedPackages(
                    "aFeedName", new CreateCuratedPackageRequest { PackageId = "anId" })
                    as ViewResult;

                Assert.NotNull(result);
                Assert.Equal("aFeedName", result.ViewBag.CuratedFeedName);
                Assert.Equal(Strings.PackageIsAlreadyCurated, controller.ModelState["PackageId"].Errors[0].ErrorMessage);
                Assert.Equal("CreateCuratedPackageForm", result.ViewName);
            }
        }
    }
}