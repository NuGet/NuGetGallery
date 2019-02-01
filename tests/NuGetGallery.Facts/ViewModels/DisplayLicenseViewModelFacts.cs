﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class DisplayLicenseViewModelFacts
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
                EmbeddedLicenseType = embeddedLicenseType,
                LicenseExpression = licenseExpression,
            };

            // act
            var model = new DisplayLicenseViewModel(package);

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
                LicenseNames = "l1,l2, l3 ,l4  ,  l5 ",
            };

            // act
            var model = new DisplayLicenseViewModel(package);

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
                LicenseUrl = licenseUrl
            };

            // act
            var model = new DisplayLicenseViewModel(package);

            // assert
            Assert.Equal(expected, model.LicenseUrl);
        }
    }
}