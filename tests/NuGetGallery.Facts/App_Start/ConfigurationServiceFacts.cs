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
                public TestableConfigurationService() : base()
                {
                    StubConfiguredSiteRoot = "http://aSiteRoot/";

                    StubRequest = new Mock<HttpRequestBase>();
                    StubRequest.Setup(stub => stub.IsLocal).Returns(false);

                    var secretReaderFactory = new EmptySecretReaderFactory();
                    SecretInjector = secretReaderFactory.CreateSecretInjector(secretReaderFactory.CreateSecretReader());
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
            public void WillUseTheConfiguredSiteRootWhenTheRequestIsLocal()
            {
                var configuration = new TestableConfigurationService();
                configuration.StubConfiguredSiteRoot = "https://theSiteRoot/";
                configuration.StubRequest.Setup(stub => stub.IsLocal).Returns(true);
                configuration.StubRequest.Setup(stub => stub.Url).Returns(new Uri("http://theLocalSiteRoot/aPath"));

                var siteRoot = configuration.GetSiteRoot(useHttps: true);

                Assert.Equal("https://theSiteRoot/", siteRoot);
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
        }

        public class TheReadSettingMethod
        {
            private class TestableConfigurationService : ConfigurationService
            {
                public TestableConfigurationService(ISecretInjector secretInjector = null)
                {
                    SecretInjector = secretInjector ?? CreateDefaultSecretInjector();
                }

                public string ConnectionStringStub { get; set; }

                public string CloudSettingStub { get; set; }

                public string AppSettingStub { get; set; }

                protected override ConnectionStringSettings GetConnectionString(string settingName)
                {
                    return new ConnectionStringSettings(ConnectionStringStub, ConnectionStringStub);
                }

                protected override string GetCloudServiceSetting(string settingName)
                {
                    return CloudSettingStub;
                }

                protected override string GetAppSetting(string settingName)
                {
                    return AppSettingStub;
                }

                private static ISecretInjector CreateDefaultSecretInjector()
                {
                    var secretReaderFactory = new EmptySecretReaderFactory();
                    return secretReaderFactory.CreateSecretInjector(secretReaderFactory.CreateSecretReader());
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
                string result = await configurationService.ReadSettingAsync("any");

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
                string result = await configurationService.ReadSettingAsync("any");

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
                
                var configurationService = new TestableConfigurationService(secretInjectorMock.Object);
                configurationService.CloudSettingStub = "somevalue";

                // Act 
                string result = await configurationService.ReadSettingAsync("any");

                // Assert
                Assert.Equal("somevalueparsed", result);
            }
        }
    }
}