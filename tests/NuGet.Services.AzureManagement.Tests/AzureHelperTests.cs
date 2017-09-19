// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Services.AzureManagement.Tests
{
    public class AzureHelperTests
    {
        [Fact]
        public void ParseCloudServiceProperties()
        {
            // Arrange
            var path = Path.Combine(Directory.GetCurrentDirectory(), "CloudServiceProperties.txt");
            string content = File.ReadAllText(path);

            // Act
            var cloudService = AzureHelper.ParseCloudServiceProperties(content);

            // Assert
            cloudService.Uri.AbsoluteUri.Equals("http://nuget-dev-0-v2v3search.cloudapp.net/");
            cloudService.InstanceCount.Equals(3);
        }

        [Fact]
        public async Task Test()
        {
            var config = new AzureConfig();
            var a = new AzureManagementAPIWrapper(config);

            var s = await a.GetTrafficManagerPropertiesAsync("fa8b3229-dc0f-4633-a285-ad597076921d", "nuget-dev-0-v2gallery", "devnugettest", CancellationToken.None);
        }
    }

    public class AzureConfig : IAzureManagementAPIWrapperConfiguration
    {
        public string ClientId => "622f5d2f-0144-4f03-8b29-4432739d54f2";

        public string ClientSecret => "";
    }
}
