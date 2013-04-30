using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Web.Mvc;
using Moq;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery
{
    public class CuratedPackagesControllerFacts
    {
        public abstract class TestableCuratedPackagesControllerBase : CuratedPackagesController
        {
            protected TestableCuratedPackagesControllerBase()
            {
                StubCuratedFeed = new CuratedFeed
                    { Key = 0, Name = "aFeedName", Managers = new HashSet<User>(new[] { new User { Username = "aUsername" } }) };
                StubIdentity = new Mock<IIdentity>();
                StubPackageRegistration = new PackageRegistration { Key = 0, Id = "anId" };

                StubIdentity.Setup(stub => stub.IsAuthenticated).Returns(true);
                StubIdentity.Setup(stub => stub.Name).Returns("aUsername");

                StubEntitiesContext = new Mock<IEntitiesContext>();
                EntitiesContext = StubEntitiesContext.Object;

                var fakeCuratedFeedSet = new FakeDbSet<CuratedFeed>();
                fakeCuratedFeedSet.Add(StubCuratedFeed);

                var fakePackageRegistrationSet = new FakeDbSet<PackageRegistration>();
                fakePackageRegistrationSet.Add(StubPackageRegistration);

                var fakeCuratedPackageSet = new FakeDbSet<CuratedPackage>();

                StubEntitiesContext
                    .Setup(m => m.Set<CuratedFeed>())
                    .Returns(fakeCuratedFeedSet);

                StubEntitiesContext
                    .Setup(m => m.Set<PackageRegistration>())
                    .Returns(fakePackageRegistrationSet);

                StubEntitiesContext
                    .Setup(m => m.Set<CuratedPackage>())
                    .Returns(fakeCuratedPackageSet);

                StubEntitiesContext
                    .Setup(m => m.PackageRegistrations)
                    .Returns(fakePackageRegistrationSet);

                var curatedFeedRepository = new EntityRepository<CuratedFeed>(
                    EntitiesContext);

                var curatedPackageRepository = new EntityRepository<CuratedPackage>(
                    EntitiesContext);

                base.CuratedFeedService = new CuratedFeedService(
                    curatedFeedRepository,
                    curatedPackageRepository);
            }

            public CuratedFeed StubCuratedFeed { get; set; }
            public Mock<IEntitiesContext> StubEntitiesContext { get; set; }
            public Mock<IIdentity> StubIdentity { get; private set; }
            public PackageRegistration StubPackageRegistration { get; private set; }

            protected override IIdentity Identity
            {
                get { return StubIdentity.Object; }
            }
        }

        public class TestableCuratedPackagesController : TestableCuratedPackagesControllerBase
        {
            public TestableCuratedPackagesController()
            {
                //StubCuratedFeedService
                //    .Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>()))
                //    .Returns(StubCuratedFeed);
            }
        }

        //public class TheDeleteCuratedPackageAction
        //{
        //    [Fact]
        //    public void WillReturn404IfTheCuratedFeedDoesNotExist()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.StubCuratedFeedService.Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>())).Returns((CuratedFeed)null);

        //        var result = controller.DeleteCuratedPackage("aCuratedFeedName", "aCuratedPackageId");

        //        Assert.IsType<HttpNotFoundResult>(result);
        //    }

        //    [Fact]
        //    public void WillReturn404IfTheCuratedPackageDoesNotExist()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.StubCuratedFeed.Packages = new[] { new CuratedPackage { PackageRegistration = new PackageRegistration() } };

        //        var result = controller.DeleteCuratedPackage("aCuratedFeedName", "aCuratedPackageId");

        //        Assert.IsType<HttpNotFoundResult>(result);
        //    }

        //    [Fact]
        //    public void WillReturn403IfTheCuratedPackageDoesNotExist()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.StubCuratedFeed.Managers = new[] { new User { Username = "notAManager" } };

        //        var result = controller.DeleteCuratedPackage("aCuratedFeedName", "aCuratedPackageId") as HttpStatusCodeResult;

        //        Assert.NotNull(result);
        //        Assert.Equal(403, result.StatusCode);
        //    }

        //    [Fact]
        //    public void WillDeleteTheCuratedPackageWhenRequestIsValid()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.StubCuratedFeed.Key = 42;
        //        controller.StubCuratedFeed.Packages = new[]
        //            {
        //                new CuratedPackage
        //                    {
        //                        Key = 1066,
        //                        PackageRegistration = new PackageRegistration { Id = "theCuratedPackageId" }
        //                    }
        //            };

        //        controller.DeleteCuratedPackage("theCuratedFeedName", "theCuratedPackageId");

        //        controller.StubCuratedFeedService.Verify(
        //            stub => stub.DeleteCuratedPackage(
        //                42,
        //                1066));
        //    }

        //    [Fact]
        //    public void WillReturn204AfterDeletingTheCuratedPackage()
        //    {
        //        var controller = new TestableCuratedPackagesController();

        //        var result = controller.DeleteCuratedPackage("aCuratedFeedName", "aCuratedPackageId") as HttpStatusCodeResult;

        //        Assert.NotNull(result);
        //        Assert.Equal(204, result.StatusCode);
        //    }

        //    public class TestableCuratedPackagesController : TestableCuratedPackagesControllerBase
        //    {
        //        public TestableCuratedPackagesController()
        //        {
        //            StubCuratedFeed.Managers = new[] { new User { Username = "aUsername" } };
        //            StubCuratedFeed.Packages = new[]
        //                {
        //                    new CuratedPackage
        //                        { PackageRegistration = new PackageRegistration { Id = "aCuratedPackageId" } }
        //                };
        //            StubCuratedFeedService
        //                .Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>()))
        //                .Returns(StubCuratedFeed);
        //        }
        //    }
        //}

        //public class TheGetCreateCuratedPackageFormAction
        //{
        //    [Fact]
        //    public void WillReturn404IfTheCuratedFeedDoesNotExist()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.StubCuratedFeedService.Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>())).Returns((CuratedFeed)null);

        //        var result = controller.GetCreateCuratedPackageForm("aFeedName");

        //        Assert.IsType<HttpNotFoundResult>(result);
        //    }

        //    [Fact]
        //    public void WillReturn403IfTheCurrentUsersIsNotAManagerOfTheCuratedFeed()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.StubIdentity.Setup(stub => stub.Name).Returns("notAManager");

        //        var result = controller.GetCreateCuratedPackageForm("aFeedName") as HttpStatusCodeResult;

        //        Assert.NotNull(result);
        //        Assert.Equal(403, result.StatusCode);
        //    }

        //    [Fact]
        //    public void WillPushTheCuratedFeedNameIntoTheViewBag()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.StubCuratedFeed.Name = "theCuratedFeedName";

        //        var result = controller.GetCreateCuratedPackageForm("aFeedName") as ViewResult;

        //        Assert.NotNull(result);
        //        Assert.Equal("theCuratedFeedName", result.ViewBag.CuratedFeedName);
        //    }

        //    public class TestableCuratedPackagesController : TestableCuratedPackagesControllerBase
        //    {
        //        public TestableCuratedPackagesController()
        //        {
        //            StubCuratedFeedService
        //                .Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>()))
        //                .Returns(StubCuratedFeed);
        //        }
        //    }
        //}

        //public class ThePatchCuratedPackageAction
        //{
        //    [Fact]
        //    public void WillReturn404IfTheCuratedFeedDoesNotExist()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.StubCuratedFeedService.Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>())).Returns((CuratedFeed)null);

        //        var result = controller.PatchCuratedPackage("aCuratedFeedName", "aCuratedPackageId", new ModifyCuratedPackageRequest());

        //        Assert.IsType<HttpNotFoundResult>(result);
        //    }

        //    [Fact]
        //    public void WillReturn404IfTheCuratedPackageDoesNotExist()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.StubCuratedFeed.Packages = new[] { new CuratedPackage { PackageRegistration = new PackageRegistration() } };

        //        var result = controller.PatchCuratedPackage("aCuratedFeedName", "aCuratedPackageId", new ModifyCuratedPackageRequest());

        //        Assert.IsType<HttpNotFoundResult>(result);
        //    }

        //    [Fact]
        //    public void WillReturn403IfTheCuratedPackageDoesNotExist()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.StubCuratedFeed.Managers = new[] { new User { Username = "notAManager" } };

        //        var result =
        //            controller.PatchCuratedPackage("aCuratedFeedName", "aCuratedPackageId", new ModifyCuratedPackageRequest()) as HttpStatusCodeResult;

        //        Assert.NotNull(result);
        //        Assert.Equal(403, result.StatusCode);
        //    }

        //    [Fact]
        //    public void WillReturn400IfTheModelStateIsInvalid()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.ModelState.AddModelError("", "anError");

        //        var result =
        //            controller.PatchCuratedPackage("aCuratedFeedName", "aCuratedPackageId", new ModifyCuratedPackageRequest()) as HttpStatusCodeResult;

        //        Assert.NotNull(result);
        //        Assert.Equal(400, result.StatusCode);
        //    }

        //    [Fact]
        //    public void WillModifyTheCuratedPackageWhenRequestIsValid()
        //    {
        //        var controller = new TestableCuratedPackagesController();
        //        controller.StubCuratedFeed.Key = 42;
        //        controller.StubCuratedFeed.Packages = new[]
        //            {
        //                new CuratedPackage
        //                    {
        //                        Key = 1066,
        //                        PackageRegistration = new PackageRegistration { Id = "theCuratedPackageId" }
        //                    }
        //            };

        //        controller.PatchCuratedPackage("theCuratedFeedName", "theCuratedPackageId", new ModifyCuratedPackageRequest { Included = true });

        //        controller.StubCuratedFeedService.Verify(
        //            stub => stub.ModifyCuratedPackage(
        //                42,
        //                1066,
        //                true));
        //    }

        //    [Fact]
        //    public void WillReturn204AfterModifyingTheCuratedPackage()
        //    {
        //        var controller = new TestableCuratedPackagesController();

        //        var result =
        //            controller.PatchCuratedPackage("aCuratedFeedName", "aCuratedPackageId", new ModifyCuratedPackageRequest()) as HttpStatusCodeResult;

        //        Assert.NotNull(result);
        //        Assert.Equal(204, result.StatusCode);
        //    }

        //    public class TestableCuratedPackagesController : TestableCuratedPackagesControllerBase
        //    {
        //        public TestableCuratedPackagesController()
        //        {
        //            StubCuratedFeed.Managers = new[] { new User { Username = "aUsername" } };
        //            StubCuratedFeed.Packages = new[]
        //                {
        //                    new CuratedPackage
        //                        { PackageRegistration = new PackageRegistration { Id = "aCuratedPackageId" } }
        //                };
        //            StubCuratedFeedService
        //                .Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>()))
        //                .Returns(StubCuratedFeed);
        //        }
        //    }
        //}

        public class ThePostCuratedPackagesAction
        {
            //[Fact]
            //public void WillReturn404IfTheCuratedFeedDoesNotExist()
            //{
            //    var controller = new TestableCuratedPackagesController();
            //    controller.StubCuratedFeedService
            //        .Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>()))
            //        .Returns((CuratedFeed)null);

            //    var result = controller.PostCuratedPackages("aFeedName", new CreateCuratedPackageRequest());

            //    Assert.IsType<HttpNotFoundResult>(result);
            //}

            //[Fact]
            //public void WillReturn403IfTheCurrentUsersIsNotAManagerOfTheCuratedFeed()
            //{
            //    var controller = new TestableCuratedPackagesController();
            //    controller.StubIdentity.Setup(stub => stub.Name).Returns("notAManager");

            //    var result = controller.PostCuratedPackages("aFeedName", new CreateCuratedPackageRequest()) as HttpStatusCodeResult;

            //    Assert.NotNull(result);
            //    Assert.Equal(403, result.StatusCode);
            //}

            //[Fact]
            //public void WillPushTheCuratedFeedNameIntoTheViewBagAndShowTheCreateCuratedPackageFormWithErrorsWhenModelStateIsInvalid()
            //{
            //    var controller = new TestableCuratedPackagesController();
            //    controller.StubCuratedFeed.Name = "theCuratedFeedName";
            //    controller.ModelState.AddModelError("", "anError");

            //    var result = controller.PostCuratedPackages("aFeedName", new CreateCuratedPackageRequest()) as ViewResult;

            //    Assert.NotNull(result);
            //    Assert.Equal("theCuratedFeedName", result.ViewBag.CuratedFeedName);
            //    Assert.Equal("CreateCuratedPackageForm", result.ViewName);
            //}

            //[Fact]
            //public void WillPushTheCuratedFeedNameIntoTheViewBagAndShowTheCreateCuratedPackageFormWithErrorsWhenThePackageIdDoesNotExist()
            //{
            //    var controller = new TestableCuratedPackagesController();
            //    controller.StubCuratedFeed.Name = "theCuratedFeedName";
            //    //controller.StubPackageRegistrationByIdQry.Setup(stub => stub.Execute(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>())).Returns(
            //    //    (PackageRegistration)null);

            //    var result = controller.PostCuratedPackages("aFeedName", new CreateCuratedPackageRequest()) as ViewResult;

            //    Assert.NotNull(result);
            //    Assert.Equal("theCuratedFeedName", result.ViewBag.CuratedFeedName);
            //    Assert.Equal(Strings.PackageWithIdDoesNotExist, controller.ModelState["PackageId"].Errors[0].ErrorMessage);
            //    Assert.Equal("CreateCuratedPackageForm", result.ViewName);
            //}

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

                Assert.True(controller.StubEntitiesContext.Object.Set<CuratedPackage>()
                    .Any(cp => cp.PackageRegistration.Id == "anId"));

                Assert.True(controller.StubEntitiesContext.Object.Set<CuratedPackage>()
                    .Any(cp => cp.Notes == "theNotes"));

                //controller.StubCuratedFeedService.Verify(
                //    stub => stub.CreatedCuratedPackage(
                //        controller.StubCuratedFeed,
                //        controller.StubPackageRegistration,
                //        true,
                //        false,
                //        "theNotes",
                //        true));
            }

            //[Fact]
            //public void WillRedirectToTheCuratedFeedRouteAfterCreatingTheCuratedPackage()
            //{
            //    var controller = new TestableCuratedPackagesController();

            //    var result = controller.PostCuratedPackages("aFeedName", new CreateCuratedPackageRequest()) as RedirectToRouteResult;

            //    Assert.NotNull(result);
            //    Assert.Equal(RouteName.CuratedFeed, result.RouteName);
            //}

            //[Fact]
            //public void WillShowAnErrorWhenThePackageHasAlreadyBeenCurated()
            //{
            //    var controller = new TestableCuratedPackagesController();
            //    controller.StubCuratedFeed.Name = "theCuratedFeedName";
            //    controller.StubCuratedFeed.Packages.Add(new CuratedPackage { PackageRegistration = new PackageRegistration { Key = 42 } });

            //    var result = controller.PostCuratedPackages("theCuratedFeedName", new CreateCuratedPackageRequest()) as ViewResult;

            //    Assert.NotNull(result);
            //    Assert.Equal("theCuratedFeedName", result.ViewBag.CuratedFeedName);
            //    Assert.Equal(Strings.PackageIsAlreadyCurated, controller.ModelState["PackageId"].Errors[0].ErrorMessage);
            //    Assert.Equal("CreateCuratedPackageForm", result.ViewName);
            //}
        }
    }
}