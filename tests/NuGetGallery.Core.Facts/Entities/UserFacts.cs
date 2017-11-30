// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        public void CloneSucceeds()
        {
            // Arrange
            var user = new User("abc")
            {
                EmailAddress = "abc@nuget.org",
                UnconfirmedEmailAddress = "def@nuget.org",
                EmailAllowed = true,
                IsDeleted = true,
                NotifyPackagePushed = true,
                PasswordResetToken = "sdfrestfertey",
                PasswordResetTokenExpirationDate = DateTime.Now,
                CreatedUtc = DateTime.UtcNow,
                LastFailedLoginUtc = DateTime.Now - TimeSpan.FromHours(1),
                FailedLoginCount = 2,
                

            };

            // Act
            var clone = user.Clone();

            // Assert
            Assert.Equal(user.Username, clone.Username);
            Assert.Equal(user.UnconfirmedEmailAddress, clone.UnconfirmedEmailAddress);
            Assert.Equal(user.EmailAddress, clone.EmailAddress);
            Assert.Equal(user.EmailAllowed, clone.EmailAllowed);
            Assert.Equal(user.IsDeleted, clone.IsDeleted);
            Assert.Equal(user.NotifyPackagePushed, clone.NotifyPackagePushed);
            Assert.Equal(user.PasswordResetToken, clone.PasswordResetToken);
            Assert.Equal(user.PasswordResetTokenExpirationDate, clone.PasswordResetTokenExpirationDate);
            Assert.Equal(user.CreatedUtc, clone.CreatedUtc);
            Assert.Equal(user.LastFailedLoginUtc, clone.LastFailedLoginUtc);
            Assert.Equal(user.FailedLoginCount, clone.FailedLoginCount);
        }
    }
}