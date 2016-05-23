// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery
{
    public class UserFacts
    {
        [Theory]
        [InlineData("Admins", true)]
        [InlineData("OtherRole", false)]
        public void IsInRoleReturnsCorrectValue(string expectedRole, bool isInRole)
        {
            // Arrange
            var user = new User("testuser");
            user.Roles.Add(new Role { Key = 1, Name = "Admins" });

            // Act
            var result = user.IsInRole(expectedRole);

            // Assert
            Assert.True(result == isInRole);
        }
    }
}