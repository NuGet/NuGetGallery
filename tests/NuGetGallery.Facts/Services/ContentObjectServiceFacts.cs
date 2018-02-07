// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using Newtonsoft.Json;
using NuGetGallery.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
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

                var loginDiscontinuationAndMigrationConfiguration = service.LoginDiscontinuationAndMigrationConfiguration as LoginDiscontinuationAndMigrationConfiguration;
                Assert.Empty(loginDiscontinuationAndMigrationConfiguration.DiscontinuedForDomains);
                Assert.Empty(loginDiscontinuationAndMigrationConfiguration.ExceptionsForEmailAddresses);
            }

            public async Task RefreshRefreshesObject()
            {
                // Arrange
                var domains = new[] { "example.com" };
                var exceptions = new[] { "test@example.com" };

                var config = new LoginDiscontinuationAndMigrationConfiguration(domains, exceptions);
                var configString = JsonConvert.SerializeObject(config);

                GetMock<IContentService>()
                    .Setup(x => x.GetContentItemAsync(Constants.ContentNames.LoginDiscontinuationAndMigrationConfiguration, It.IsAny<TimeSpan>()))
                    .Returns(Task.FromResult<IHtmlString>(new HtmlString(configString)));

                var service = GetService<ContentObjectService>();

                // Act
                await service.Refresh();
                var loginDiscontinuationAndMigrationConfiguration = service.LoginDiscontinuationAndMigrationConfiguration as LoginDiscontinuationAndMigrationConfiguration;

                // Assert
                Assert.True(loginDiscontinuationAndMigrationConfiguration.DiscontinuedForDomains.SequenceEqual(domains));
                Assert.True(loginDiscontinuationAndMigrationConfiguration.ExceptionsForEmailAddresses.SequenceEqual(exceptions));
            }
        }
    }
}
