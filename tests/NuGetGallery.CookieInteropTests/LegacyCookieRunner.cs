// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.DataHandler;
using Microsoft.Owin.Security.Interop;
using Microsoft.Owin.Testing;
using NuGetGallery.Authentication;
using Owin;

namespace NuGetGallery.CookieInteropTests
{
    internal static class LegacyCookieRunner
    {
        internal static async Task IssueAsync(string artifactDirectory)
        {
            string keyRing = Path.Combine(artifactDirectory, CoreConstants.Folders.DataProtectionFolderName);
            AspNetTicketDataFormat sharedFormat = CreateSharedFormat(keyRing);
            ClaimsPrincipal authenticatedPrincipal = null;

            using (TestServer server = CreateServer(sharedFormat, principal => authenticatedPrincipal = principal))
            {
                string[] normalHeaders = await IssueCookieAsync(server, oversized: false);
                string[] largeHeaders = await IssueCookieAsync(server, oversized: true);

                Program.Assert(normalHeaders.Length == 1, "A normal Katana ticket should use one cookie.");
                Program.Assert(GetCookieValue(normalHeaders, SharedCookieConstants.CookieName).Length > 0, "Katana did not issue the shared cookie.");
                AssertCookiePath(normalHeaders);
                Program.Assert(GetCookieValue(largeHeaders, SharedCookieConstants.CookieName).StartsWith("chunks-", StringComparison.Ordinal), "Katana did not use Core-compatible chunks-N framing.");

                HttpStatusCode largeStatus = await AuthenticateAsync(server, Program.ToCookieHeader(largeHeaders));
                Program.Assert(largeStatus == HttpStatusCode.OK, "Katana did not reassemble its oversized shared cookie.");
                Program.AssertPrincipal(authenticatedPrincipal);

                string normalCookie = GetCookieValue(normalHeaders, SharedCookieConstants.CookieName);
                AssertLifetime(sharedFormat.Unprotect(normalCookie));
                Program.Write(artifactDirectory, "legacy.cookie", normalCookie);
                Program.WriteLines(artifactDirectory, "legacy-large.headers", largeHeaders);
                Program.Write(artifactDirectory, "legacy-expired.cookie", Protect(sharedFormat, DateTimeOffset.UtcNow.AddHours(-8), DateTimeOffset.UtcNow.AddHours(-2)));
                Program.Write(artifactDirectory, "legacy-tampered.cookie", Tamper(normalCookie));

                AspNetTicketDataFormat wrongPurposeFormat = CreateSharedFormat(
                    keyRing,
                    SharedCookieConstants.AuthenticationScheme + ".wrong");
                Program.Write(
                    artifactDirectory,
                    "legacy-wrong-purpose.cookie",
                    Protect(wrongPurposeFormat, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Add(SharedCookieConstants.Expiration)));

                AspNetTicketDataFormat wrongKeyFormat = CreateSharedFormat(Path.Combine(artifactDirectory, "wrong-keys"));
                Program.Write(
                    artifactDirectory,
                    "legacy-wrong-key.cookie",
                    Protect(wrongKeyFormat, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Add(SharedCookieConstants.Expiration)));

                var machineKeyFormat = new TicketDataFormat(new MachineKeyDataProtector(
                    typeof(CookieAuthenticationMiddleware).FullName,
                    SharedCookieConstants.AuthenticationScheme,
                    "v1"));
                string machineKeyCookie = Protect(machineKeyFormat, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Add(SharedCookieConstants.Expiration));
                Program.Assert(sharedFormat.Unprotect(machineKeyCookie) == null, "The previous machine-key cookie unexpectedly used the shared format.");
                Program.Write(artifactDirectory, "legacy-machine-key.cookie", machineKeyCookie);
            }
        }

        internal static async Task AcceptAsync(string artifactDirectory)
        {
            AspNetTicketDataFormat sharedFormat = CreateSharedFormat(
                Path.Combine(artifactDirectory, CoreConstants.Folders.DataProtectionFolderName));
            ClaimsPrincipal authenticatedPrincipal = null;

            using (TestServer server = CreateServer(sharedFormat, principal => authenticatedPrincipal = principal))
            {
                string issuedCookie = Program.Read(artifactDirectory, "core-issued.cookie");
                string refreshedCookie = Program.Read(artifactDirectory, "core-refreshed.cookie");
                await AssertAuthenticatedAsync(server, issuedCookie, () => authenticatedPrincipal);
                await AssertAuthenticatedAsync(server, refreshedCookie, () => authenticatedPrincipal);
                await AssertAuthenticatedAsync(server, Program.ToCookieHeader(Program.ReadLines(artifactDirectory, "core-large.headers")), () => authenticatedPrincipal);

                AuthenticationTicket issuedTicket = sharedFormat.Unprotect(issuedCookie);
                AuthenticationTicket refreshedTicket = sharedFormat.Unprotect(refreshedCookie);
                AuthenticationTicket legacyTicket = sharedFormat.Unprotect(Program.Read(artifactDirectory, "legacy.cookie"));
                AssertLifetime(issuedTicket);
                AssertLifetime(refreshedTicket);
                Program.Assert(
                    refreshedTicket.Properties.IssuedUtc > legacyTicket.Properties.IssuedUtc,
                    "ASP.NET Core did not advance the issue time when sliding-refreshing the Katana cookie.");

                HttpStatusCode expiredStatus = await AuthenticateAsync(
                    server,
                    SharedCookieConstants.CookieName + "=" + Program.Read(artifactDirectory, "core-expired.cookie"));
                Program.Assert(expiredStatus == HttpStatusCode.Unauthorized, "Katana accepted an expired ASP.NET Core cookie.");
            }
        }

        private static TestServer CreateServer(AspNetTicketDataFormat format, Action<ClaimsPrincipal> onAuthenticated)
        {
            return TestServer.Create(app =>
            {
                app.UseCookieAuthentication(new CookieAuthenticationOptions
                {
                    AuthenticationType = SharedCookieConstants.AuthenticationScheme,
                    AuthenticationMode = AuthenticationMode.Active,
                    CookieName = SharedCookieConstants.CookieName,
                    CookiePath = SharedCookieConstants.CookiePath,
                    CookieHttpOnly = true,
                    ExpireTimeSpan = SharedCookieConstants.Expiration,
                    SlidingExpiration = SharedCookieConstants.SlidingExpiration,
                    TicketDataFormat = format,
                    CookieManager = new Microsoft.Owin.Security.Interop.ChunkingCookieManager(),
                });

                app.Run(async context =>
                {
                    if (context.Request.Path.Value == "/issue")
                    {
                        bool oversized = string.Equals(context.Request.Query["large"], "true", StringComparison.Ordinal);
                        var properties = new AuthenticationProperties
                        {
                            IsPersistent = true,
                            IssuedUtc = DateTimeOffset.UtcNow.AddHours(-4),
                            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2),
                        };
                        context.Authentication.SignIn(properties, Program.CreateIdentity(oversized));
                        await context.Response.WriteAsync("issued");
                        return;
                    }

                    ClaimsPrincipal principal = context.Authentication.User;
                    if (principal?.Identity?.IsAuthenticated == true)
                    {
                        onAuthenticated(principal);
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        await context.Response.WriteAsync("authenticated");
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    }
                });
            });
        }

        private static async Task<string[]> IssueCookieAsync(TestServer server, bool oversized)
        {
            using (HttpResponseMessage response = await server.HttpClient.GetAsync(oversized ? "/issue?large=true" : "/issue"))
            {
                Program.Assert(response.IsSuccessStatusCode, "Katana cookie issuance failed.");
                return response.Headers.GetValues("Set-Cookie").ToArray();
            }
        }

        private static async Task AssertAuthenticatedAsync(
            TestServer server,
            string cookieOrHeader,
            Func<ClaimsPrincipal> getPrincipal)
        {
            string cookieHeader = cookieOrHeader.Contains("=")
                ? cookieOrHeader
                : SharedCookieConstants.CookieName + "=" + cookieOrHeader;
            HttpStatusCode status = await AuthenticateAsync(server, cookieHeader);
            Program.Assert(status == HttpStatusCode.OK, "Katana rejected an ASP.NET Core cookie.");
            Program.AssertPrincipal(getPrincipal());
        }

        private static async Task<HttpStatusCode> AuthenticateAsync(TestServer server, string cookieHeader)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/authenticate"))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
                using (HttpResponseMessage response = await server.HttpClient.SendAsync(request))
                {
                    return response.StatusCode;
                }
            }
        }

        private static AspNetTicketDataFormat CreateSharedFormat(string keyRing, string scheme = SharedCookieConstants.AuthenticationScheme)
        {
            Directory.CreateDirectory(keyRing);
            Microsoft.AspNetCore.DataProtection.IDataProtectionProvider provider = DataProtectionProvider.Create(
                new DirectoryInfo(keyRing),
                builder => builder.SetApplicationName(SharedCookieConstants.DataProtectionApplicationName));
            Microsoft.AspNetCore.DataProtection.IDataProtector protector = provider.CreateProtector(
                SharedCookieConstants.DataProtectionMiddlewarePurpose,
                scheme,
                SharedCookieConstants.DataProtectionFormatPurpose);
            return new AspNetTicketDataFormat(new DataProtectorShim(protector));
        }

        private static string Protect(
            ISecureDataFormat<AuthenticationTicket> format,
            DateTimeOffset issuedUtc,
            DateTimeOffset expiresUtc)
        {
            var properties = new AuthenticationProperties
            {
                IsPersistent = true,
                IssuedUtc = issuedUtc,
                ExpiresUtc = expiresUtc,
            };
            return format.Protect(new AuthenticationTicket(Program.CreateIdentity(), properties));
        }

        private static string GetCookieValue(IEnumerable<string> setCookieHeaders, string cookieName)
        {
            string prefix = cookieName + "=";
            string header = setCookieHeaders.Single(value => value.StartsWith(prefix, StringComparison.Ordinal));
            string pair = header.Split(';')[0];
            return pair.Substring(prefix.Length);
        }

        private static string Tamper(string value)
        {
            int index = value.Length / 2;
            char replacement = value[index] == 'A' ? 'B' : 'A';
            return value.Substring(0, index) + replacement + value.Substring(index + 1);
        }

        private static void AssertCookiePath(IEnumerable<string> setCookieHeaders)
        {
            Program.Assert(
                setCookieHeaders.All(header => header.IndexOf("path=" + SharedCookieConstants.CookiePath, StringComparison.OrdinalIgnoreCase) >= 0),
                "Katana did not emit the shared cookie path.");
        }

        private static void AssertLifetime(AuthenticationTicket ticket)
        {
            Program.Assert(ticket != null, "The shared ticket could not be read.");
            Program.Assert(ticket.Properties.IssuedUtc.HasValue, "The shared ticket has no issue time.");
            Program.Assert(ticket.Properties.ExpiresUtc.HasValue, "The shared ticket has no expiration time.");
            Program.Assert(
                ticket.Properties.ExpiresUtc.Value - ticket.Properties.IssuedUtc.Value == SharedCookieConstants.Expiration,
                "The shared ticket did not preserve the six-hour lifetime.");
        }

        private sealed class MachineKeyDataProtector : Microsoft.Owin.Security.DataProtection.IDataProtector
        {
            private readonly string[] _purposes;

            internal MachineKeyDataProtector(params string[] purposes)
            {
                _purposes = purposes;
            }

            public byte[] Protect(byte[] userData)
            {
                return MachineKey.Protect(userData, _purposes);
            }

            public byte[] Unprotect(byte[] protectedData)
            {
                return MachineKey.Unprotect(protectedData, _purposes);
            }
        }
    }
}
