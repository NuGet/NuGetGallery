// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Auditing;
using Xunit;

namespace NuGetGallery.Services
{
    public class StatusServiceFacts
    {
        [Fact]
        public void ValidateServicesThatWillBeInStatusValidation()
        {
            // Arrange
            var cloudBlobFileStorageServiceIsStatusParticipant = typeof(ICloudStorageStatusDependency).IsAssignableFrom(typeof(CloudBlobFileStorageService));
            var cloudAuditingServiceIsStatusParticipant = typeof(ICloudStorageStatusDependency).IsAssignableFrom(typeof(CloudAuditingService));

            // Assert
            Assert.True(cloudBlobFileStorageServiceIsStatusParticipant);
            Assert.True(cloudAuditingServiceIsStatusParticipant);
        }
    }
}