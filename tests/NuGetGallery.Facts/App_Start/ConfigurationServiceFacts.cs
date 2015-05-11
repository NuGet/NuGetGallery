// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Web;
using Moq;
using NuGetGallery.Configuration;
using Xunit;

namespace NuGetGallery.App_Start
{
    public class ConfigurationServiceFacts
    {
        public class TestableConfigurationService : ConfigurationService
        {
            public TestableConfigurationService()
            {
                StubRequest = new Mock<HttpRequestBase>();
                StubConfiguredSiteRoot = "http://aSiteRoot/";
                Current = (StubConfiguration = new Mock<IAppConfiguration>()).Object;
                StubConfiguration.Setup(c => c.SiteRoot).Returns(() => StubConfiguredSiteRoot);

                StubRequest.Setup(stub => stub.IsLocal).Returns(false);
            }

            public Mock<IAppConfiguration> StubConfiguration { get; set; }
            public string StubConfiguredSiteRoot { get; set; }
            public Mock<HttpRequestBase> StubRequest { get; set; }

            protected override HttpRequestBase GetCurrentRequest()
            {
                return StubRequest.Object;
            }
        }

        public class TheGetSiteRootMethod
        {
            [Fact]
            public void WillGetTheConfiguredHttpSiteRoot()
            {
                var configuration = new TestableConfigurationService();
                configuration.StubConfiguredSiteRoot = "http://theSiteRoot/";

                var siteRoot = configuration.GetSiteRoot(useHttps: false);

                Assert.Equal("http://theSiteRoot/", siteRoot);
            }

            [Fact]
            public void WillGetTheConfiguredHttpsSiteRoot()
            {
                var configuration = new TestableConfigurationService();
                configuration.StubConfiguredSiteRoot = "http://theSiteRoot/";

                var siteRoot = configuration.GetSiteRoot(useHttps: true);

                Assert.Equal("https://theSiteRoot/", siteRoot);
            }

            [Fact]
            public void WillUseTheActualRootWhenTheRequestIsLocal()
            {
                var configuration = new TestableConfigurationService();
                configuration.StubRequest.Setup(stub => stub.IsLocal).Returns(true);
                configuration.StubRequest.Setup(stub => stub.Url).Returns(new Uri("http://theLocalSiteRoot/aPath"));

                var siteRoot = configuration.GetSiteRoot(useHttps: true);

                Assert.Equal("https://thelocalsiteroot/", siteRoot);
            }

            [Fact]
            public void WillUseHttpUponRequestWhenConfiguredSiteRootIsHttps()
            {
                var configuration = new TestableConfigurationService();
                configuration.StubConfiguredSiteRoot = "https://theSiteRoot/";

                var siteRoot = configuration.GetSiteRoot(useHttps: false);

                Assert.Equal("http://theSiteRoot/", siteRoot);
            }

            [Fact]
            public void WillThrowIfConfiguredSiteRootIsNotHttpOrHttps()
            {
                var configuration = new TestableConfigurationService();
                configuration.StubConfiguredSiteRoot = "ftp://theSiteRoot/";

                Assert.Throws<InvalidOperationException>(() => configuration.GetSiteRoot(useHttps: false));
            }

            [Fact]
            public void WillCacheTheSiteRootLookup()
            {
                var configuration = new TestableConfigurationService();
                configuration.GetSiteRoot(useHttps: false);

                configuration.GetSiteRoot(useHttps: true);

                configuration.StubRequest.Verify(stub => stub.IsLocal, Times.Once());
            }
        }
    }
}