// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Web;
using Moq;
using NuGet.Services.KeyVault;
using NuGetGallery.Configuration;
using NuGetGallery.Configuration.SecretReader;
using Xunit;

namespace NuGetGallery.App_Start
{
    public class ConfigurationServiceFacts
    {
        public class TheGetSiteRootMethod
        {
            private class TestableConfigurationService : ConfigurationService
            {
                public TestableConfigurationService() : base(new EmptySecretReaderFactory())
                {
                    StubConfiguredSiteRoot = "http://aSiteRoot/";

                    StubRequest = new Mock<HttpRequestBase>();
                    StubRequest.Setup(stub => stub.IsLocal).Returns(false);
                }

                public string StubConfiguredSiteRoot { get; set; }
                public Mock<HttpRequestBase> StubRequest { get; set; }

                protected override string GetAppSetting(string settingName)
                {
                    var tempAppConfig = new AppConfiguration();

                    if (settingName == $"{SettingPrefix}{nameof(tempAppConfig.SiteRoot)}")
                    {
                        return StubConfiguredSiteRoot;
                    }

                    return string.Empty;
                }

                protected override HttpRequestBase GetCurrentRequest()
                {
                    return StubRequest.Object;
                }
            }

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

                // A task that is started by GetSiteRoot throws an InvalidOperationException
                // but it propagates back to here as an AggregateException.
                Assert.Throws<AggregateException>(() => configuration.GetSiteRoot(useHttps: false));
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

        public class TheReadSettingMethod
        {
            private class TestableConfigurationService : ConfigurationService
            {
                public TestableConfigurationService(ISecretReaderFactory secretReaderFactory = null) 
                    : base(secretReaderFactory ?? new EmptySecretReaderFactory())
                {
                }

                public string ConnectionStringStub { get; set; }

                public string CloudSettingStub { get; set; }

                public string AppSettingStub { get; set; }

                protected override ConnectionStringSettings GetConnectionString(string settingName)
                {
                    return new ConnectionStringSettings(ConnectionStringStub, ConnectionStringStub);
                }

                protected override string GetCloudSetting(string settingName)
                {
                    return CloudSettingStub;
                }

                protected override string GetAppSetting(string settingName)
                {
                    return AppSettingStub;
                }
            }

            [Fact]
            public async Task WhenCloudSettingIsNullStringNullIsReturned()
            {
                // Arrange
                var configurationService = new TestableConfigurationService();
                configurationService.CloudSettingStub = "null";
                configurationService.AppSettingStub = "bla";
                configurationService.ConnectionStringStub = "abc";

                // Act 
                string result = await configurationService.ReadSetting("any");

                // Assert
                Assert. Null(result);
            }

            [Fact]
            public async Task WhenCloudSettingIsEmptyAppSettingIsReturned()
            {
                // Arrange
                var configurationService = new TestableConfigurationService();
                configurationService.CloudSettingStub = null;
                configurationService.AppSettingStub = string.Empty;
                configurationService.ConnectionStringStub = "abc";

                // Act 
                string result = await configurationService.ReadSetting("any");

                // Assert
                Assert.Equal(configurationService.ConnectionStringStub, result);
            }

            [Fact]
            public async Task WhenSettingIsNotEmptySecretInjectorIsRan()
            {
                // Arrange
                var secretInjectorMock = new Mock<ISecretInjector>();
                secretInjectorMock.Setup(x => x.InjectAsync(It.IsAny<string>()))
                                  .Returns<string>(s => Task.FromResult(s + "parsed"));

                var secretReaderFactory = new Mock<ISecretReaderFactory>();
                secretReaderFactory.Setup(x => x.CreateSecretReader())
                    .Returns(new EmptySecretReader());
                secretReaderFactory.Setup(x => x.CreateSecretInjector(It.IsAny<ISecretReader>()))
                    .Returns(secretInjectorMock.Object);

                var configurationService = new TestableConfigurationService(secretReaderFactory.Object);
                configurationService.CloudSettingStub = "somevalue";

                // Act 
                string result = await configurationService.ReadSetting("any");

                // Assert
                Assert.Equal("somevalueparsed", result);
            }
        }
    }
}