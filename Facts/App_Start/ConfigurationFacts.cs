using System;
using System.Web;
using Moq;
using Xunit;

namespace NuGetGallery.App_Start
{
    public class ConfigurationFacts
    {
        public class TheGetSiteRootMethod
        {
            [Fact]
            public void WillGetTheConfiguredHttpSiteRoot()
            {
                var configuration = new TestableConfiguration();
                configuration.StubConfiguredSiteRoot = "http://theSiteRoot/";

                var siteRoot = configuration.GetSiteRoot(useHttps: false);

                Assert.Equal("http://theSiteRoot/", siteRoot);
            }

            [Fact]
            public void WillGetTheConfiguredHttpsSiteRoot()
            {
                var configuration = new TestableConfiguration();
                configuration.StubConfiguredSiteRoot = "http://theSiteRoot/";

                var siteRoot = configuration.GetSiteRoot(useHttps: true);

                Assert.Equal("https://theSiteRoot/", siteRoot);
            }

            [Fact]
            public void WillUseTheActualRootWhenTheRequestIsLocal()
            {
                var configuration = new TestableConfiguration();
                configuration.StubRequest.Setup(stub => stub.IsLocal).Returns(true);
                configuration.StubRequest.Setup(stub => stub.Url).Returns(new Uri("http://theLocalSiteRoot/aPath"));

                var siteRoot = configuration.GetSiteRoot(useHttps: true);

                Assert.Equal("https://thelocalsiteroot/", siteRoot);
            }

            [Fact]
            public void WillUseHttpUponRequestWhenConfiguredSiteRootIsHttps()
            {
                var configuration = new TestableConfiguration();
                configuration.StubConfiguredSiteRoot = "https://theSiteRoot/";

                var siteRoot = configuration.GetSiteRoot(useHttps: false);

                Assert.Equal("http://theSiteRoot/", siteRoot);
            }

            [Fact]
            public void WillThrowIfConfiguredSiteRootIsNotHttpOrHttps()
            {
                var configuration = new TestableConfiguration();
                configuration.StubConfiguredSiteRoot = "ftp://theSiteRoot/";

                Assert.Throws<InvalidOperationException>(() => configuration.GetSiteRoot(useHttps: false));
            }

            [Fact]
            public void WillCacheTheSiteRootLookup()
            {
                var configuration = new TestableConfiguration();
                configuration.GetSiteRoot(useHttps: false);
                
                configuration.GetSiteRoot(useHttps: true);

                configuration.StubRequest.Verify(stub => stub.IsLocal, Times.Once());
            }
        }

        public class TestableConfiguration : Configuration
        {
            public TestableConfiguration()
            {
                StubRequest = new Mock<HttpRequestBase>();
                StubConfiguredSiteRoot = "http://aSiteRoot/";
                
                StubRequest.Setup(stub => stub.IsLocal).Returns(false);
            }

            public string StubConfiguredSiteRoot { get; set; }
            public Mock<HttpRequestBase> StubRequest { get; set; }

            protected override string GetConfiguredSiteRoot()
            {
                return StubConfiguredSiteRoot;
            }
            
            protected override HttpRequestBase GetCurrentRequest()
            {
                return StubRequest.Object;
            }
        }
    }
}
