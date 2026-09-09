// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Interop;
using NuGetGallery.Authentication.Providers.Cookie;
using Xunit;

namespace NuGetGallery.Authentication
{
    public class LocalUserAuthenticatorFacts
    {
        public class TheCreateCookieAuthenticationOptionsMethod
        {
            [Theory]
            [InlineData(false, CookieSecureOption.Never)]
            [InlineData(true, CookieSecureOption.Always)]
            public void UsesTheSharedCookieContractAndPreservesLegacyBehavior(
                bool requireSsl,
                CookieSecureOption expectedSecureOption)
            {
                string keyRingPath = CreateKeyRingPath();
                try
                {
                    IDataProtectionProvider provider = DataProtectionProvider.Create(
                        new DirectoryInfo(keyRingPath),
                        builder => builder.SetApplicationName(SharedCookieConstants.DataProtectionApplicationName));
                    var authenticator = new LocalUserAuthenticator(provider);

                    CookieAuthenticationOptions options = authenticator.CreateCookieAuthenticationOptions(requireSsl);

                    Assert.Equal(SharedCookieConstants.AuthenticationScheme, options.AuthenticationType);
                    Assert.Equal(AuthenticationMode.Active, options.AuthenticationMode);
                    Assert.Equal(SharedCookieConstants.CookieName, options.CookieName);
                    Assert.Equal(SharedCookieConstants.CookiePath, options.CookiePath);
                    Assert.True(options.CookieHttpOnly);
                    Assert.Equal(expectedSecureOption, options.CookieSecure);
                    Assert.Equal(new PathString("/users/account/LogOn"), options.LoginPath);
                    Assert.Equal(SharedCookieConstants.Expiration, options.ExpireTimeSpan);
                    Assert.Equal(SharedCookieConstants.SlidingExpiration, options.SlidingExpiration);
                    Assert.IsType<AspNetTicketDataFormat>(options.TicketDataFormat);
                    Assert.IsType<Microsoft.Owin.Security.Interop.ChunkingCookieManager>(options.CookieManager);

                    var identity = new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Name, "legacy-user") },
                        SharedCookieConstants.AuthenticationScheme);
                    var ticket = new AuthenticationTicket(
                        identity,
                        new AuthenticationProperties
                        {
                            IssuedUtc = DateTimeOffset.UtcNow,
                            ExpiresUtc = DateTimeOffset.UtcNow.Add(SharedCookieConstants.Expiration),
                        });

                    string protectedTicket = options.TicketDataFormat.Protect(ticket);
                    AuthenticationTicket roundTrippedTicket = options.TicketDataFormat.Unprotect(protectedTicket);

                    Assert.NotNull(roundTrippedTicket);
                    Assert.Equal("legacy-user", roundTrippedTicket.Identity.Name);
                }
                finally
                {
                    if (Directory.Exists(keyRingPath))
                    {
                        Directory.Delete(keyRingPath, recursive: true);
                    }
                }
            }

            [Fact]
            public void FailsInsteadOfFallingBackToTheMachineKeyFormat()
            {
                var authenticator = new LocalUserAuthenticator();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => authenticator.CreateCookieAuthenticationOptions(requireSsl: true));

                Assert.Contains("Data Protection provider", exception.Message);
            }

            [Fact]
            public void RejectsAnAuthenticationTypeThatWouldBreakTheSharedPurpose()
            {
                string keyRingPath = CreateKeyRingPath();
                try
                {
                    IDataProtectionProvider provider = DataProtectionProvider.Create(
                        new DirectoryInfo(keyRingPath),
                        builder => builder.SetApplicationName(SharedCookieConstants.DataProtectionApplicationName));
                    var authenticator = new LocalUserAuthenticator(provider);
                    authenticator.BaseConfig.AuthenticationType = "DifferentScheme";

                    var exception = Assert.Throws<InvalidOperationException>(
                        () => authenticator.CreateCookieAuthenticationOptions(requireSsl: true));

                    Assert.Contains("shared cookie scheme", exception.Message);
                }
                finally
                {
                    if (Directory.Exists(keyRingPath))
                    {
                        Directory.Delete(keyRingPath, recursive: true);
                    }
                }
            }

            private static string CreateKeyRingPath()
            {
                return Path.Combine(
                    AppContext.BaseDirectory,
                    "legacy-local-user-keys-" + Guid.NewGuid().ToString("N"));
            }
        }
    }
}
