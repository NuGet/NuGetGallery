// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.FeatureFlags;
using Xunit;

namespace NuGetGallery.Features
{
    public class FeatureFlagClientExtensionsFacts
    {
        [Fact]
        public void ConvertsUser()
        {
            // Arrange
            var client = new Mock<IFeatureFlagClient>();

            var user = new User
            {
                Username = "a",
                EmailAddress = "a@a.com",
                Roles = new List<Role>()
            };

            var admin = new User
            {
                Username = "b",
                EmailAddress = "b@b.com",
                Roles = new List<Role>
                {
                    new Role { Name = Constants.AdminRoleName }
                }
            };

            // Act
            FeatureFlagClientExtensions.IsEnabled(client.Object, "flightA", user, defaultValue: true);
            FeatureFlagClientExtensions.IsEnabled(client.Object, "flightB", admin, defaultValue: true);

            // Assert
            client.Verify(
                c => c.IsEnabled(
                    "flightA",
                    It.Is<IFlightUser>(u =>
                        u.Username == "a" &&
                        u.EmailAddress == "a@a.com" &&
                        u.IsSiteAdmin == false),
                    true));

            client.Verify(
                c => c.IsEnabled(
                    "flightB",
                    It.Is<IFlightUser>(u =>
                        u.Username == "b" &&
                        u.EmailAddress == "b@b.com" &&
                        u.IsSiteAdmin == true),
                    true));
        }
    }
}
