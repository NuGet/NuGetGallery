// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGetGallery.Entities
{
    public class CredentialFacts
    {
        [Fact]
        public void CloneSucceeds()
        {
            // Arrange
            var credential = new Credential()
            {
                Key = 1,
                UserKey = 2,
                Type = "key",
                Value = "bla",
                Description = "best key ever",
                Identity = "def",
                Created = DateTime.Now,
                Expires = DateTime.UtcNow,
                ExpirationTicks = 10,
                LastUsed = DateTime.Now - TimeSpan.FromDays(1)
            };


            // Act
            var clone = credential.Clone();

            // Assert
            Assert.Equal(credential.Key, clone.Key);
            Assert.Equal(credential.UserKey, clone.UserKey);
            Assert.Equal(credential.Type, clone.Type);
            Assert.Equal(credential.Value, clone.Value);
            Assert.Equal(credential.Description, clone.Description);
            Assert.Equal(credential.Identity, clone.Identity);
            Assert.Equal(credential.Created, clone.Created);
            Assert.Equal(credential.Expires, clone.Expires);
            Assert.Equal(credential.ExpirationTicks, clone.ExpirationTicks);
            Assert.Equal(credential.LastUsed, clone.LastUsed);
        }
    }
}
