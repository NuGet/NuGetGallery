using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Web.Mvc;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class CuratedFeedsControllerFacts
    {
        public class TestableCuratedFeedsController : CuratedFeedsController
        {
            public TestableCuratedFeedsController()
            {
                StubCuratedFeed = new CuratedFeed
                    { Key = 0, Name = "aName", Managers = new HashSet<User>(new[] { new User { Username = "aUsername" } }) };
                StubCuratedFeedService = new Mock<ICuratedFeedService>();
                StubIdentity = new Mock<IIdentity>();

                StubIdentity.Setup(stub => stub.IsAuthenticated).Returns(true);
                StubIdentity.Setup(stub => stub.Name).Returns("aUsername");
                StubCuratedFeedService
                    .Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns(StubCuratedFeed);
            }

            public CuratedFeed StubCuratedFeed { get; set; }
            public Mock<ICuratedFeedService> StubCuratedFeedService { get; private set; }
            public Mock<IIdentity> StubIdentity { get; private set; }

            protected override IIdentity Identity
            {
                get { return StubIdentity.Object; }
            }

            protected override T GetService<T>()
            {
                if (typeof(T) == typeof(ICuratedFeedService))
                {
                    return (T)StubCuratedFeedService.Object;
                }

                throw new Exception("Tried to get an unexpected service.");
            }
        }

        public class TheGetCuratedFeedAction
        {
            [Fact]
            public void WillReturn404IfTheCuratedFeedDoesNotExist()
            {
                var controller = new TestableCuratedFeedsController();
                controller.StubCuratedFeedService.Setup(stub => stub.GetFeedByName(It.IsAny<string>(), It.IsAny<bool>())).Returns((CuratedFeed)null);

                var result = controller.CuratedFeed("aFeedName");

                Assert.IsType<HttpNotFoundResult>(result);
            }

            [Fact]
            public void WillReturn403IfTheCurrentUsersIsNotAManagerOfTheCuratedFeed()
            {
                var controller = new TestableCuratedFeedsController();
                controller.StubIdentity.Setup(stub => stub.Name).Returns("notAManager");

                var result = controller.CuratedFeed("aFeedName") as HttpStatusCodeResult;

                Assert.NotNull(result);
                Assert.Equal(403, result.StatusCode);
            }

            [Fact]
            public void WillPassTheCuratedFeedNameToTheView()
            {
                var controller = new TestableCuratedFeedsController();
                controller.StubCuratedFeed.Name = "theCuratedFeedName";

                var viewModel = (controller.CuratedFeed("aFeedName") as ViewResult).Model as CuratedFeedViewModel;

                Assert.NotNull(viewModel);
                Assert.Equal("theCuratedFeedName", viewModel.Name);
            }

            [Fact]
            public void WillPassTheCuratedFeedManagersToTheView()
            {
                var controller = new TestableCuratedFeedsController();
                controller.StubIdentity.Setup(stub => stub.Name).Returns("theManager");
                controller.StubCuratedFeed.Managers = new HashSet<User>(new[] { new User { Username = "theManager" } });

                var viewModel = (controller.CuratedFeed("aFeedName") as ViewResult).Model as CuratedFeedViewModel;

                Assert.NotNull(viewModel);
                Assert.Equal("theManager", viewModel.Managers.First());
            }

            [Fact]
            public void WillPassTheIncludedPackagesToTheView()
            {
                var controller = new TestableCuratedFeedsController();
                controller.StubCuratedFeed.Packages = new HashSet<CuratedPackage>(
                    new[]
                        {
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = true,
                                    Included = true,
                                    PackageRegistration = new PackageRegistration { Id = "theAutomaticallyCuratedId" }
                                },
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = false,
                                    Included = true,
                                    PackageRegistration = new PackageRegistration { Id = "theManuallyCuratedId" }
                                },
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = true,
                                    Included = false,
                                    PackageRegistration = new PackageRegistration { Id = "theExcludedId" }
                                }
                        });

                var viewModel = (controller.CuratedFeed("aFeedName") as ViewResult).Model as CuratedFeedViewModel;

                Assert.NotNull(viewModel);
                Assert.Equal(2, viewModel.IncludedPackages.Count());
                Assert.Equal("theAutomaticallyCuratedId", viewModel.IncludedPackages.ElementAt(0).Id);
                Assert.Equal("theManuallyCuratedId", viewModel.IncludedPackages.ElementAt(1).Id);
            }

            [Fact]
            public void WillPassTheExcludedPackagesToTheView()
            {
                var controller = new TestableCuratedFeedsController();
                controller.StubCuratedFeed.Packages = new HashSet<CuratedPackage>(
                    new[]
                        {
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = true,
                                    Included = true,
                                    PackageRegistration = new PackageRegistration { Id = "theAutomaticallyCuratedId" }
                                },
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = false,
                                    Included = true,
                                    PackageRegistration = new PackageRegistration { Id = "theManuallyCuratedId" }
                                },
                            new CuratedPackage
                                {
                                    AutomaticallyCurated = true,
                                    Included = false,
                                    PackageRegistration = new PackageRegistration { Id = "theExcludedId" }
                                }
                        });

                var viewModel = (controller.CuratedFeed("aFeedName") as ViewResult).Model as CuratedFeedViewModel;

                Assert.NotNull(viewModel);
                Assert.Equal(1, viewModel.ExcludedPackages.Count());
                Assert.Equal("theExcludedId", viewModel.ExcludedPackages.First());
            }
        }
    }
}