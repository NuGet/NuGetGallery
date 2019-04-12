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

            // Act
            FeatureFlagClientExtensions.IsEnabled(client.Object, "flight", user, defaultValue: true);

            // Assert
            client.Verify(
                c => c.IsEnabled(
                    "flight",
                    It.Is<IFlightUser>(u =>
                        u.Username == "a" &&
                        u.EmailAddress == "a@a.com" &&
                        u.IsSiteAdmin == false),
                    true));
        }

        [Fact]
        public void ConvertsAnonymousUser()
        {
            // Arrange - anonymous users are represented as a null user object
            var client = new Mock<IFeatureFlagClient>();

            User user = null;

            // Act
            FeatureFlagClientExtensions.IsEnabled(client.Object, "flight", user, defaultValue: true);

            // Assert
            client.Verify(
                c => c.IsEnabled(
                    "flight",
                    It.Is<IFlightUser>(u => u == null),
                    true));
        }

        [Fact]
        public void ConvertsAdmins()
        {
            // Arrange
            var client = new Mock<IFeatureFlagClient>();

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
            FeatureFlagClientExtensions.IsEnabled(client.Object, "flight", admin, defaultValue: true);

            // Assert
            client.Verify(
                c => c.IsEnabled(
                    "flight",
                    It.Is<IFlightUser>(u =>
                        u.Username == "b" &&
                        u.EmailAddress == "b@b.com" &&
                        u.IsSiteAdmin == true),
                    true));
        }
    }
}
