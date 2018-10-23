// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Services.Entities.Tests
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

        [Fact]
        public void UserEquality()
        {
            // Arrange
            var user1 = new User("user1") { Key = 1 };
            var user2 = new User("user2") { Key = 1 };
            var user3 = new User("user2") { Key = 3 };

            // Assert
            Assert.True(user1 == user2);
            Assert.True(user1.Equals(user2));
            Assert.True(user3 != user2);
            Assert.True(user1.GetHashCode() == user2.GetHashCode());
        }
    }
}