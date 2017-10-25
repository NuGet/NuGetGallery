// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Xunit;

namespace NuGetGallery
{
    public class OrganizationFacts
    {
        [Fact]
        public void Name_ReturnsAccountUsername()
        {
            // Arrange
            var org = new Organization()
            {
                Account = new User()
                {
                    Username = "Org"
                }
            };

            // Act & Assert
            Assert.Equal("Org", org.Name);
        }

        [Fact]
        public void Administrators_ReturnsAdminsFromMemberships()
        {
            // Arrange
            var org = new Organization();
            org.Memberships = new []
            {
                CreateMembership(org, "User1", true),
                CreateMembership(org, "User2", false),
                CreateMembership(org, "User3", true)
            };

            // Act
            var admins = org.Administrators.ToArray();

            // Assert
            Assert.Equal(2, admins.Length);
            Assert.Equal("User1", admins[0].Username);
            Assert.Equal("User3", admins[1].Username);
        }

        [Fact]
        public void Members_ReturnsAdminsAndCollaboratorsFromMemberships()
        {
            // Arrange
            var org = new Organization();
            org.Memberships = new[]
            {
                CreateMembership(org, "User1", true),
                CreateMembership(org, "User2", false),
                CreateMembership(org, "User3", true)
            };

            // Act
            var admins = org.Members.ToArray();

            // Assert
            Assert.Equal(3, admins.Length);
            Assert.Equal("User1", admins[0].Username);
            Assert.Equal("User2", admins[1].Username);
            Assert.Equal("User3", admins[2].Username);
        }

        private Membership CreateMembership(Organization organization, string user, bool isAdmin)
        {
            return new Membership()
            {
                Organization = organization,
                Member = new User(user),
                IsAdmin = isAdmin
            };
        }
    }
}
