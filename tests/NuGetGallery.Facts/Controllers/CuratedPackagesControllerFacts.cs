// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery
{
    public class CuratedPackagesControllerFacts
    {
        public class TestableCuratedPackagesController : CuratedPackagesController
        {
            public Fakes Fakes { get; }

            public TestableCuratedPackagesController()
            {
                Fakes = new Fakes();

                StubCuratedFeed = new CuratedFeed
                    { Key = 0, Name = "aFeedName", Managers = new HashSet<User>(new[] { Fakes.User }) };
                StubPackageRegistration = new PackageRegistration { Key = 0, Id = "anId" };

                SetOwinContextOverride(Fakes.CreateOwinContext());

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

                var httpContext = new Mock<HttpContextBase>();
                TestUtility.SetupHttpContextMockForUrlGeneration(httpContext, this);
            }

            public CuratedFeed StubCuratedFeed { get; set; }
            public PackageRegistration StubPackageRegistration { get; private set; }
        }

        public class TheDeleteCuratedPackageAction
        {
            [Fact]
            public async Task WillReturn404IfTheCuratedFeedDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);

                var result = await controller.DeleteCuratedPackage("aStrangeCuratedFeedName", "anId");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public async Task WillReturn404IfTheCuratedPackageDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);
                controller.StubCuratedFeed.Packages = new[] { new CuratedPackage { PackageRegistration = new PackageRegistration() } };

                var result = await controller.DeleteCuratedPackage("aFeedName", "aStrangeCuratedPackageId");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public async Task WillReturn403IfTheUserNotAManager()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.Owner);

                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                    });

                var result = await controller.DeleteCuratedPackage("aFeedName", "anId") as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(403, result.StatusCode);
            }

            [Fact]
            public async Task WillDeleteTheCuratedPackageWhenRequestIsValid()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);

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

                await controller.DeleteCuratedPackage("aFeedName", "anId");

                Assert.False(controller.EntitiesContext.CuratedPackages.Any
                    (cp => cp.PackageRegistration.Id == "anId"));
            }

            [Fact]
            public async Task WillReturn204AfterDeletingTheCuratedPackage()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);

                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                    });

                var result = await controller.DeleteCuratedPackage("aFeedName", "anId") as HttpStatusCodeResult;

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
                controller.SetCurrentUser(controller.Fakes.User);

                var result = controller.GetCreateCuratedPackageForm("aWrongFeedName");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WillReturn403IfTheCurrentUsersIsNotAManagerOfTheCuratedFeed()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.Owner);

                var result = controller.GetCreateCuratedPackageForm("aFeedName") as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(403, result.StatusCode);
            }

            [Fact]
            public void WillPushTheCuratedFeedNameIntoTheViewBag()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);
                controller.StubCuratedFeed.Name = "theCuratedFeedName";

                var result = controller.GetCreateCuratedPackageForm("theCuratedFeedName") as ViewResult;

                Assert.NotNull(result);
                Assert.Equal("theCuratedFeedName", result.ViewBag.CuratedFeedName);
            }
        }

        public class ThePatchCuratedPackageAction
        {
            [Fact]
            public async Task WillReturn404IfTheCuratedFeedDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);

                var result = await controller.PatchCuratedPackage("aWrongFeedName", "anId",
                    new ModifyCuratedPackageRequest());

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public async Task WillReturn404IfTheCuratedPackageDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);

                var result = await controller.PatchCuratedPackage("aFeedName", "aWrongId", new ModifyCuratedPackageRequest());

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public async Task WillReturn403IfNotAFeedManager()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.Owner);
                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                    });

                var result = await controller.PatchCuratedPackage("aFeedName", "anId",
                        new ModifyCuratedPackageRequest()) as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(403, result.StatusCode);
            }

            [Fact]
            public async Task WillReturn400IfTheModelStateIsInvalid()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);
                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                    });
                controller.ModelState.AddModelError("", "anError");

                var result = await controller.PatchCuratedPackage(
                    "aFeedName",
                    "anId",
                    new ModifyCuratedPackageRequest()) as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(400, result.StatusCode);
            }

            [Fact]
            public async Task WillModifyTheCuratedPackageWhenRequestIsValid()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);
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

                var result = await controller.PatchCuratedPackage(
                    "aFeedName",
                    "anId",
                    new ModifyCuratedPackageRequest { Included = false }) as HttpStatusCodeResult;

                Assert.True(controller.StubCuratedFeed.Packages.Any(
                    cp => cp.Included == false));
            }

            [Fact]
            public async Task WillReturn204AfterModifyingTheCuratedPackage()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);
                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage
                    {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key,
                        Included = true,
                    });

                var result = await controller.PatchCuratedPackage("aFeedName", "anId",
                    new ModifyCuratedPackageRequest()) as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(204, result.StatusCode);
            }
        }

        public class ThePostCuratedPackagesAction
        {
            [Fact]
            public async Task WillReturn404IfTheCuratedFeedDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);

                var result = await controller.PostCuratedPackages(
                    "aWrongFeedName",
                    new CreateCuratedPackageRequest { PackageId = "AnId" });

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public async Task WillReturn403IfTheCurrentUsersIsNotAManagerOfTheCuratedFeed()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.Owner);

                var result = await controller.PostCuratedPackages(
                    "aFeedName",
                    new CreateCuratedPackageRequest { PackageId = "AnId" })
                    as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(403, result.StatusCode);
            }

            [Fact]
            public async Task WillPushTheCuratedFeedNameIntoTheViewBagAndShowTheCreateCuratedPackageFormWithErrorsWhenModelStateIsInvalid()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);
                controller.StubCuratedFeed.Name = "theCuratedFeedName";
                controller.ModelState.AddModelError("", "anError");

                var result = await controller.PostCuratedPackages(
                    "theCuratedFeedName", new CreateCuratedPackageRequest()) as ViewResult;

                Assert.NotNull(result);
                Assert.Equal("theCuratedFeedName", result.ViewBag.CuratedFeedName);
                Assert.Equal("CreateCuratedPackageForm", result.ViewName);
            }

            [Fact]
            public async Task WillPushTheCuratedFeedNameIntoTheViewBagAndShowTheCreateCuratedPackageFormWithErrorsWhenThePackageIdDoesNotExist()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);

                var result = await controller.PostCuratedPackages("aFeedName",
                    new CreateCuratedPackageRequest { PackageId = "aWrongId" }) as ViewResult;

                Assert.NotNull(result);
                Assert.Equal("aFeedName", result.ViewBag.CuratedFeedName);
                Assert.Equal(Strings.PackageWithIdDoesNotExist, controller.ModelState["PackageId"].Errors[0].ErrorMessage);
                Assert.Equal("CreateCuratedPackageForm", result.ViewName);
            }

            [Fact]
            public async Task WillCreateTheCuratedPackage()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);

                await controller.PostCuratedPackages(
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
            public async Task WillRedirectToTheCuratedFeedRouteAfterCreatingTheCuratedPackage()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);

                var result = await controller.PostCuratedPackages(
                    "aFeedName", new CreateCuratedPackageRequest { PackageId = "anId" })
                    as RedirectToRouteResult;

                Assert.NotNull(result);
                Assert.Equal(RouteName.CuratedFeed, result.RouteName);
            }

            [Fact]
            public async Task WillShowAnErrorWhenThePackageHasAlreadyBeenCurated()
            {
                var controller = new TestableCuratedPackagesController();
                controller.SetCurrentUser(controller.Fakes.User);
                controller.StubCuratedFeed.Packages.Add(
                    new CuratedPackage {
                        CuratedFeed = controller.StubCuratedFeed,
                        CuratedFeedKey = controller.StubCuratedFeed.Key,
                        PackageRegistration = controller.StubPackageRegistration,
                        PackageRegistrationKey = controller.StubPackageRegistration.Key
                    });

                var result = await controller.PostCuratedPackages(
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