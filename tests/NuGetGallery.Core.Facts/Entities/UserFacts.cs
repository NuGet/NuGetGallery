// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
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

        [Fact]
        public void IsOrganization_ReturnsTrueIfOrganizationIsNotNull()
        {
            // Arrange
            var user = new User()
            {
                Organization = new Organization()
            };

            // Act & Assert
            Assert.True(user.IsOrganization);
        }

        [Fact]
        public void IsOrganization_ReturnsFalseIfOrganizationIsNull()
        {
            // Arrange
            var user = new User();

            // Act & Assert
            Assert.False(user.IsOrganization);
        }

        public void Organizations_ReturnsOrganizationsFromMemberships()
        {
            // Arrange
            var user = new User("User");
            user.Memberships = new[]
            {
                CreateMembership(user, "Org1"),
                CreateMembership(user, "Org2"),
                CreateMembership(user, "Org3")
            };

            // Act
            var organizations = user.Organizations.ToArray();

            // Assert
            Assert.Equal(3, organizations.Length);
            Assert.Equal("Org1", organizations[0].Name);
            Assert.Equal("Org2", organizations[1].Name);
            Assert.Equal("Org3", organizations[2].Name);
        }

        private Membership CreateMembership(User user, string organization)
        {
            return new Membership()
            {
                Organization = new Organization()
                {
                    Account = new User(organization)
                }
            };
        }
    }
}