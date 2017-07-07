// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
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
    }
}
