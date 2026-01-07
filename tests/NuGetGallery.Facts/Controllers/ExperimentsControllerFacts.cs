// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class ExperimentsControllerFacts
    {
        public class GetSearchSideBySide : Facts
        {
            [Fact]
            public async Task CallsDependencies()
            {
                await Target.SearchSideBySide(SearchTerm);

                FeatureFlagService.Verify(
                    x => x.IsSearchSideBySideEnabled(TestUtility.FakeUser),
                    Times.Once);
                SearchSideBySideService.Verify(
                    x => x.SearchAsync(SearchTerm, TestUtility.FakeUser),
                    Times.Once);
            }

            [Fact]
            public async Task ReturnsViewResult()
            {
                SearchSideBySideService
                    .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<User>()))
                    .ReturnsAsync(() => ViewModel);

                var result = await Target.SearchSideBySide(SearchTerm);

                var model = ResultAssert.IsView<SearchSideBySideViewModel>(result);
                Assert.Same(ViewModel, model);
            }

            [Fact]
            public async Task ReturnsIsDisabledWhenFeatureFlagIsDisabled()
            {
                FeatureFlagService
                    .Setup(x => x.IsSearchSideBySideEnabled(It.IsAny<User>()))
                    .Returns(false);

                var result = await Target.SearchSideBySide(SearchTerm);

                var model = ResultAssert.IsView<SearchSideBySideViewModel>(result);
                Assert.True(model.IsDisabled);
            }
        }

        public class PostSearchSideBySide : Facts
        {
            [Fact]
            public async Task CallsDependencies()
            {
                await Target.SearchSideBySide(ViewModel);

                FeatureFlagService.Verify(
                    x => x.IsSearchSideBySideEnabled(TestUtility.FakeUser),
                    Times.Once);
                SearchSideBySideService.Verify(
                    x => x.RecordFeedbackAsync(ViewModel, "https://localhost/experiments/search-sxs?q=json"),
                    Times.Once);
            }

            [Fact]
            public async Task SetsTempDataAndRedirects()
            {
                var result = await Target.SearchSideBySide(ViewModel);

                Assert.Equal(new[] { "Message" }, Target.TempData.Keys.ToArray());
                Assert.Equal("Thank you for providing feedback! Feel free to try some other queries.", Target.TempData["Message"]);

                var redirect = Assert.IsType<RedirectToRouteResult>(result);
                Assert.Equal(new[] { "action" }, redirect.RouteValues.Keys.ToArray());
                Assert.Equal(nameof(Target.SearchSideBySide), redirect.RouteValues["action"]);
            }

            [Fact]
            public async Task ReturnsNotFoundWhenFeatureFlagIsDisabled()
            {
                FeatureFlagService
                    .Setup(x => x.IsSearchSideBySideEnabled(It.IsAny<User>()))
                    .Returns(false);

                var result = await Target.SearchSideBySide(ViewModel);

                Assert.IsType<HttpNotFoundResult>(result);
            }
        }

        public abstract class Facts : TestContainer
        {
            public Facts()
            {
                SearchSideBySideService = new Mock<ISearchSideBySideService>();
                FeatureFlagService = new Mock<IFeatureFlagService>();
                HttpContext = new Mock<HttpContextBase>();

                FeatureFlagService.SetReturnsDefault(true);

                SearchTerm = "json";
                ViewModel = new SearchSideBySideViewModel
                {
                    SearchTerm = SearchTerm,
                };

                Target = new ExperimentsController(
                    SearchSideBySideService.Object,
                    FeatureFlagService.Object);

                TestUtility.SetupHttpContextMockForUrlGeneration(HttpContext, Target);
                Target.SetOwinContextOverride(Fakes.CreateOwinContext());
                Target.SetCurrentUser(TestUtility.FakeUser);
            }

            public Mock<ISearchSideBySideService> SearchSideBySideService { get; }
            public Mock<IFeatureFlagService> FeatureFlagService { get; }
            public Mock<HttpContextBase> HttpContext { get; }
            public string SearchTerm { get; set; }
            public SearchSideBySideViewModel ViewModel { get; }
            public ExperimentsController Target { get; }
        }
    }
}
