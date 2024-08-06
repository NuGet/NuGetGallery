﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Services.Entities;
using NuGet.Services.Licenses;
using System.Collections.Generic;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class DisplayLicenseViewModelFactoryFacts
    {
        [Theory]
        [InlineData(EmbeddedLicenseFileType.Absent, "some expression")]
        [InlineData(EmbeddedLicenseFileType.Markdown, "some expression")]
        [InlineData(EmbeddedLicenseFileType.PlainText, "some expression")]
        [InlineData(EmbeddedLicenseFileType.PlainText, null)]
        public void ItInitializesLicenseFileTypeAndLicenseExpression(EmbeddedLicenseFileType embeddedLicenseType, string licenseExpression)
        {
            // arrange
            var package = new Package
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                EmbeddedLicenseType = embeddedLicenseType,
                LicenseExpression = licenseExpression,
            };

            // act
            var model = CreateDisplayLicenseViewModel(package, licenseExpressionSegments: null, licenseFileContents: null);

            // assert
            Assert.Equal(embeddedLicenseType, model.EmbeddedLicenseType);
            Assert.Equal(licenseExpression, model.LicenseExpression);
        }

        [Fact]
        public void LicenseNamesAreParsedByCommas()
        {
            // arrange
            var licenseUrl = "https://mylicense/";
            var package = new Package
            {
                LicenseUrl = licenseUrl,
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                LicenseNames = "l1,l2, l3 ,l4  ,  l5 ",
            };

            // act
            var model = CreateDisplayLicenseViewModel(package, licenseExpressionSegments: null, licenseFileContents: null);

            // assert
            Assert.Equal(new string[] { "l1", "l2", "l3", "l4", "l5" }, model.LicenseNames);
            Assert.Equal(licenseUrl, model.LicenseUrl);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("not a url", null)]
        [InlineData("git://github.com/notavalidscheme", null)]
        [InlineData("http://www.microsoft.com/web/webpi/eula/webpages_2_eula_enu.htm", "https://www.microsoft.com/web/webpi/eula/webpages_2_eula_enu.htm")]
        [InlineData("http://aspnetwebstack.codeplex.com/license", "https://aspnetwebstack.codeplex.com/license")]
        [InlineData("http://go.microsoft.com/?linkid=9809688", "https://go.microsoft.com/?linkid=9809688")]
        [InlineData("http://github.com/url", "https://github.com/url")]
        [InlineData("http://githubpages.github.io/my.page/license.html", "https://githubpages.github.io/my.page/license.html")]
        [InlineData("http://githubpages.github.com", "https://githubpages.github.com/")]
        public void ItInitializesLicenseUrl(string licenseUrl, string expected)
        {
            // arrange
            var package = new Package
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                LicenseUrl = licenseUrl
            };

            // act
            var model = CreateDisplayLicenseViewModel(package, licenseExpressionSegments: null, licenseFileContents: null);

            // assert
            Assert.Equal(expected, model.LicenseUrl);
        }

        [Fact]
        public void ItInitializesLicenseExpressionSegments()
        {
            // arrange
            var package = new Package
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
            };
            var segments = new List<CompositeLicenseExpressionSegment>();

            // act
            var model = CreateDisplayLicenseViewModel(package, licenseExpressionSegments: segments, licenseFileContents: null);

            // assert
            Assert.Equal(segments, model.LicenseExpressionSegments);
        }

        [Fact]
        public void ItInitializesLicenseFileContents()
        {
            // arrange
            var package = new Package
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
            };
            var licenseFileContents = "It's a license";

            // act
            var model = CreateDisplayLicenseViewModel(package, licenseExpressionSegments: null, licenseFileContents: licenseFileContents);

            // assert
            Assert.Equal(licenseFileContents, model.LicenseFileContents);
        }

        [Fact]
        public void ItInitializesLicenseFileContentsWithEmbeddedMarkdownLicense()
        {
            // arrange
            Mock<IFeatureFlagService> _featureFlagService = new Mock<IFeatureFlagService>();
            Mock<IIconUrlProvider> _iconUrlProvider = new Mock<IIconUrlProvider>();
            Mock<IMarkdownService> _markdownService = new Mock<IMarkdownService>();
            User user = new User();

            DisplayLicenseViewModelFactory displayLicenseViewModelFactory = new DisplayLicenseViewModelFactory(_iconUrlProvider.Object,
                _markdownService.Object,
                _featureFlagService.Object);

            var licenseFileContents = "It's a license";
            var package = new Package
            {
                Version = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "SomeId" },
                EmbeddedLicenseType = EmbeddedLicenseFileType.Markdown,
            };

            RenderedMarkdownResult expectedlicenseContentResult = new RenderedMarkdownResult
            {
                Content = licenseFileContents,
                ImagesRewritten = false,
                ImageSourceDisallowed = false,
            };

            _featureFlagService.Setup(x => x.IsLicenseMdRenderingEnabled(user))
                .Returns(true);
            _markdownService.Setup(x => x.GetHtmlFromMarkdown(licenseFileContents))
                .Returns(expectedlicenseContentResult);
            
            // act
            var model = displayLicenseViewModelFactory.Create(package, null, licenseFileContents, user);

            // assert
            Assert.NotNull(model.LicenseFileContentsHtml);
            Assert.Equal(expectedlicenseContentResult.Content,
                model.LicenseFileContentsHtml.Content);
            Assert.Equal(expectedlicenseContentResult.ImageSourceDisallowed,
                model.LicenseFileContentsHtml.ImageSourceDisallowed);
            Assert.Equal(expectedlicenseContentResult.ImagesRewritten,
                model.LicenseFileContentsHtml.ImagesRewritten);
        }

        private static DisplayLicenseViewModel CreateDisplayLicenseViewModel(
            Package package,
            IReadOnlyCollection<CompositeLicenseExpressionSegment> licenseExpressionSegments = null,
            string licenseFileContents = null)
        {
            return new DisplayLicenseViewModelFactory(Mock.Of<IIconUrlProvider>(), Mock.Of<IMarkdownService>(), Mock.Of<IFeatureFlagService>()).Create(
                package,
                licenseExpressionSegments,
                licenseFileContents,
                null);
        }
    }
}