// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Moq;
using Newtonsoft.Json;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Services
{
    public class ContentObjectServiceFacts
    {
        public class TheRefreshMethod : TestContainer
        {
            [Fact]
            public void ObjectsHaveDefaultStateIfNotRefreshed()
            {
                var service = new ContentObjectService(new Mock<IContentService>().Object);

                var loginDiscontinuationAndMigrationConfiguration = service.LoginDiscontinuationConfiguration as LoginDiscontinuationConfiguration;
                Assert.Empty(loginDiscontinuationAndMigrationConfiguration.DiscontinuedForDomains);
                Assert.Empty(loginDiscontinuationAndMigrationConfiguration.ExceptionsForEmailAddresses);

                var certificatesConfiguration = service.CertificatesConfiguration as CertificatesConfiguration;

                Assert.False(certificatesConfiguration.IsUIEnabledByDefault);
                Assert.Empty(certificatesConfiguration.AlwaysEnabledForDomains);
                Assert.Empty(certificatesConfiguration.AlwaysEnabledForEmailAddresses);

                var symbolsConfiguration = service.SymbolsConfiguration as SymbolsConfiguration;

                Assert.False(symbolsConfiguration.IsSymbolsUploadEnabledForAll);
                Assert.Empty(symbolsConfiguration.AlwaysEnabledForDomains);
                Assert.Empty(symbolsConfiguration.AlwaysEnabledForEmailAddresses);

                var typosquattingConfiguration = service.TyposquattingConfiguration as TyposquattingConfiguration;

                Assert.Equal(TyposquattingConfiguration.DefaultPackageIdCheckListLength, typosquattingConfiguration.PackageIdChecklistLength);
                Assert.False(typosquattingConfiguration.IsCheckEnabled);
                Assert.False(typosquattingConfiguration.IsBlockUsersEnabled);
                Assert.Equal(TyposquattingConfiguration.DefaultPackageIdChecklistCacheExpireTimeInHours, typosquattingConfiguration.PackageIdChecklistCacheExpireTimeInHours);
            }

            [Fact]
            public async Task RefreshRefreshesObject()
            {
                // Arrange
                var emails = new[] { "discontinued@different.com" };
                var domains = new[] { "example.com" };
                var exceptions = new[] { "exception@example.com" };
                var shouldTransforms = new[] { "transfomer@example.com" };
                var orgTenantPairs = new[] { new OrganizationTenantPair("example.com", "tenantId") };

                var loginDiscontinuationConfiguration = new LoginDiscontinuationConfiguration(emails, domains, exceptions, shouldTransforms, orgTenantPairs, isPasswordDiscontinuedForAll: false);
                var loginJson = JsonConvert.SerializeObject(loginDiscontinuationConfiguration);

                var isUIEnabledByDefault = true;
                var alwaysEnabledForDomains = new[] { "a" };
                var alwaysEnabledForEmailAddresses = new[] { "b" };

                var packageIdChecklistLength = 20000;
                var packageIdChecklistCacheExpireTimeInHours = 12.0;

                var certificatesConfiguration = new CertificatesConfiguration(
                    isUIEnabledByDefault,
                    alwaysEnabledForDomains,
                    alwaysEnabledForEmailAddresses);
                var certificatesJson = JsonConvert.SerializeObject(certificatesConfiguration);

                var symbolsConfiguration = new SymbolsConfiguration(
                    isSymbolsUploadEnabledForAll: true,
                    alwaysEnabledForDomains: alwaysEnabledForDomains,
                    alwaysEnabledForEmailAddresses: alwaysEnabledForEmailAddresses);
                var symbolsJson = JsonConvert.SerializeObject(symbolsConfiguration);

                var typosquattingConfiguration = new TyposquattingConfiguration(
                    packageIdChecklistLength: packageIdChecklistLength,
                    isCheckEnabled: true,
                    isBlockUsersEnabled: true,
                    packageIdChecklistCacheExpireTimeInHours: packageIdChecklistCacheExpireTimeInHours);
                var typosquattingJson = JsonConvert.SerializeObject(typosquattingConfiguration);

                var contentService = GetMock<IContentService>();

                contentService
                    .Setup(x => x.GetContentItemAsync(GalleryConstants.ContentNames.LoginDiscontinuationConfiguration, It.IsAny<TimeSpan>()))
                    .Returns(Task.FromResult<IHtmlString>(new HtmlString(loginJson)));

                contentService
                    .Setup(x => x.GetContentItemAsync(GalleryConstants.ContentNames.CertificatesConfiguration, It.IsAny<TimeSpan>()))
                    .Returns(Task.FromResult<IHtmlString>(new HtmlString(certificatesJson)));

                contentService
                    .Setup(x => x.GetContentItemAsync(GalleryConstants.ContentNames.SymbolsConfiguration, It.IsAny<TimeSpan>()))
                    .Returns(Task.FromResult<IHtmlString>(new HtmlString(symbolsJson)));

                contentService
                    .Setup(x => x.GetContentItemAsync(GalleryConstants.ContentNames.TyposquattingConfiguration, It.IsAny<TimeSpan>()))
                    .Returns(Task.FromResult<IHtmlString>(new HtmlString(typosquattingJson)));

                var service = GetService<ContentObjectService>();

                // Act
                await service.Refresh();

                loginDiscontinuationConfiguration = service.LoginDiscontinuationConfiguration as LoginDiscontinuationConfiguration;
                certificatesConfiguration = service.CertificatesConfiguration as CertificatesConfiguration;
                symbolsConfiguration = service.SymbolsConfiguration as SymbolsConfiguration;
                typosquattingConfiguration = service.TyposquattingConfiguration as TyposquattingConfiguration;

                // Assert
                Assert.True(loginDiscontinuationConfiguration.DiscontinuedForEmailAddresses.SequenceEqual(emails));
                Assert.True(loginDiscontinuationConfiguration.DiscontinuedForDomains.SequenceEqual(domains));
                Assert.True(loginDiscontinuationConfiguration.ExceptionsForEmailAddresses.SequenceEqual(exceptions));
                Assert.True(loginDiscontinuationConfiguration.EnabledOrganizationAadTenants.SequenceEqual(orgTenantPairs, new OrganizationTenantPairComparer()));

                Assert.True(certificatesConfiguration.IsUIEnabledByDefault);
                Assert.Equal(alwaysEnabledForDomains, certificatesConfiguration.AlwaysEnabledForDomains);
                Assert.Equal(alwaysEnabledForEmailAddresses, certificatesConfiguration.AlwaysEnabledForEmailAddresses);

                Assert.True(symbolsConfiguration.IsSymbolsUploadEnabledForAll);
                Assert.Equal(alwaysEnabledForDomains, symbolsConfiguration.AlwaysEnabledForDomains);
                Assert.Equal(alwaysEnabledForEmailAddresses, symbolsConfiguration.AlwaysEnabledForEmailAddresses);

                Assert.Equal(packageIdChecklistLength, typosquattingConfiguration.PackageIdChecklistLength);
                Assert.True(typosquattingConfiguration.IsCheckEnabled);
                Assert.True(typosquattingConfiguration.IsBlockUsersEnabled);
                Assert.Equal(packageIdChecklistCacheExpireTimeInHours, typosquattingConfiguration.PackageIdChecklistCacheExpireTimeInHours);
            }
        }
    }
}