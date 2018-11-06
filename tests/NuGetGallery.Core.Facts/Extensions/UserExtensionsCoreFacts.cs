// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Extensions
{
    public class UserExtensionsCoreFacts
    {
        public class TheToMailAddressMethod
        {
            [Fact]
            public void WhenUserIsNull_ThrowsArgumentNullException()
            {
                Assert.Throws<ArgumentNullException>(() =>
                {
                    UserExtensionsCore.ToMailAddress(null);
                });
            }

            [Fact]
            public void WhenConfirmed_ReturnsEmailAddress()
            {
                // User.Confirmed is calculated property based on EmailAddress
                var user = new User("confirmed")
                {
                    EmailAddress = "confirmed@example.com"
                };

                var mailAddress = user.ToMailAddress();

                Assert.Equal("confirmed@example.com", mailAddress.Address);
                Assert.Equal("confirmed", mailAddress.User);
            }

            [Fact]
            public void WhenNotConfirmed_ReturnsUnconfirmedEmailAddress()
            {
                // User.Confirmed is calculated property based on EmailAddress
                var user = new User("unconfirmed")
                {
                    UnconfirmedEmailAddress = "unconfirmed@example.com"
                };

                var mailAddress = user.ToMailAddress();

                Assert.Equal("unconfirmed@example.com", mailAddress.Address);
                Assert.Equal("unconfirmed", mailAddress.User);
            }
        }
    }
}
