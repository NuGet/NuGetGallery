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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NuGetGallery.Authentication;
using NuGetGallery.DataProtection;

namespace NuGetGallery.CookieInteropTests
{
    internal static class CoreCookieRunner
    {
        internal static async Task ExchangeAsync(string artifactDirectory)
        {
            ClaimsPrincipal authenticatedPrincipal = null;
            WebApplication app = CreateApplication(
                Path.Combine(artifactDirectory, CoreConstants.Folders.DataProtectionFolderName),
                principal => authenticatedPrincipal = principal);

            await app.StartAsync();
            try
            {
                HttpClient client = app.GetTestClient();

                string legacyCookie = Program.Read(artifactDirectory, "legacy.cookie");
                string[] refreshedHeaders = await AssertAuthenticatedAsync(
                    client,
                    SharedCookieConstants.CookieName + "=" + legacyCookie,
                    () => authenticatedPrincipal);
                Program.Assert(refreshedHeaders.Length == 1, "ASP.NET Core did not sliding-refresh the aged Katana cookie.");
                Program.Write(
                    artifactDirectory,
                    "core-refreshed.cookie",
                    GetCookieValue(refreshedHeaders, SharedCookieConstants.CookieName));

                await AssertRejectedAsync(client, Program.Read(artifactDirectory, "legacy-expired.cookie"), "expired");
                await AssertRejectedAsync(client, Program.Read(artifactDirectory, "legacy-tampered.cookie"), "tampered");
                await AssertRejectedAsync(client, Program.Read(artifactDirectory, "legacy-wrong-purpose.cookie"), "wrong-purpose");
                await AssertRejectedAsync(client, Program.Read(artifactDirectory, "legacy-wrong-key.cookie"), "wrong-key");
                await AssertRejectedAsync(client, Program.Read(artifactDirectory, "legacy-machine-key.cookie"), "machine-key");

                string[] legacyLargeHeaders = Program.ReadLines(artifactDirectory, "legacy-large.headers");
                await AssertAuthenticatedAsync(client, Program.ToCookieHeader(legacyLargeHeaders), () => authenticatedPrincipal);

                string[] issuedHeaders = await IssueCookieAsync(client, "/issue");
                Program.Assert(issuedHeaders.Length == 1, "A normal ASP.NET Core ticket should use one cookie.");
                AssertCookiePath(issuedHeaders);
                Program.Write(
                    artifactDirectory,
                    "core-issued.cookie",
                    GetCookieValue(issuedHeaders, SharedCookieConstants.CookieName));

                string[] largeHeaders = await IssueCookieAsync(client, "/issue?large=true");
                Program.Assert(GetCookieValue(largeHeaders, SharedCookieConstants.CookieName).StartsWith("chunks-", StringComparison.Ordinal), "ASP.NET Core did not use chunks-N framing.");
                await AssertAuthenticatedAsync(client, Program.ToCookieHeader(largeHeaders), () => authenticatedPrincipal);
                Program.WriteLines(artifactDirectory, "core-large.headers", largeHeaders);

                string[] expiredHeaders = await IssueCookieAsync(client, "/issue?expired=true");
                Program.Write(
                    artifactDirectory,
                    "core-expired.cookie",
                    GetCookieValue(expiredHeaders, SharedCookieConstants.CookieName));
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        private static WebApplication CreateApplication(string keyRing, Action<ClaimsPrincipal> onAuthenticated)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            string storageRoot = Directory.GetParent(keyRing).FullName;
            var storage = new FileSystemFileStorageService(storageRoot, new FileSystemService());
            var repository = new FileStorageXmlRepository(storage);
            var dataProtectionConfiguration = new SharedDataProtectionConfiguration
            {
                StorageType = DataProtectionStorageType.FileSystem,
                StorageLocation = storageRoot,
                ApplicationDiscriminator = SharedCookieConstants.DataProtectionApplicationName,
                KeyLifetime = TimeSpan.FromDays(90),
                EncryptKeysAtRest = false,
                KeyVaultKeyRotationPeriod = TimeSpan.FromDays(30),
                KeyRingRetentionPeriod = TimeSpan.FromDays(365),
                KeyVaultKeyRetentionPeriod = TimeSpan.FromDays(365),
            };
            builder.Services
                .AddDataProtection()
                .ConfigureSharedDataProtection(
                    repository,
                    dataProtectionConfiguration,
                    keyEncryptionKeyResolver: null,
                    isProduction: false);
            builder.Services
                .AddAuthentication(SharedCookieConstants.AuthenticationScheme)
                .AddCookie(SharedCookieConstants.AuthenticationScheme, options =>
                {
                    options.Cookie.Name = SharedCookieConstants.CookieName;
                    options.Cookie.Path = SharedCookieConstants.CookiePath;
                    options.Cookie.HttpOnly = true;
                    options.ExpireTimeSpan = SharedCookieConstants.Expiration;
                    options.SlidingExpiration = SharedCookieConstants.SlidingExpiration;
                });

            WebApplication app = builder.Build();
            app.UseAuthentication();
            app.Run(async context =>
            {
                if (context.Request.Path == "/issue")
                {
                    bool oversized = string.Equals(context.Request.Query["large"], "true", StringComparison.Ordinal);
                    bool expired = string.Equals(context.Request.Query["expired"], "true", StringComparison.Ordinal);
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                    {
                        IsPersistent = true,
                        IssuedUtc = expired ? now.AddHours(-8) : now,
                        ExpiresUtc = expired ? now.AddHours(-2) : now.Add(SharedCookieConstants.Expiration),
                    };
                    await context.SignInAsync(
                        SharedCookieConstants.AuthenticationScheme,
                        new ClaimsPrincipal(Program.CreateIdentity(oversized)),
                        properties);
                    await context.Response.WriteAsync("issued");
                    return;
                }

                if (context.User.Identity?.IsAuthenticated == true)
                {
                    onAuthenticated(context.User);
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.WriteAsync("authenticated");
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                }
            });
            return app;
        }

        private static async Task<string[]> AssertAuthenticatedAsync(
            HttpClient client,
            string cookieHeader,
            Func<ClaimsPrincipal> getPrincipal)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/authenticate"))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
                using (HttpResponseMessage response = await client.SendAsync(request))
                {
                    Program.Assert(response.StatusCode == HttpStatusCode.OK, "ASP.NET Core rejected a Katana cookie.");
                    Program.AssertPrincipal(getPrincipal());
                    return GetSetCookieHeaders(response);
                }
            }
        }

        private static async Task AssertRejectedAsync(HttpClient client, string cookie, string caseName)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/authenticate"))
            {
                request.Headers.TryAddWithoutValidation("Cookie", SharedCookieConstants.CookieName + "=" + cookie);
                using (HttpResponseMessage response = await client.SendAsync(request))
                {
                    Program.Assert(response.StatusCode == HttpStatusCode.Unauthorized, $"ASP.NET Core accepted the {caseName} Katana cookie.");
                }
            }
        }

        private static async Task<string[]> IssueCookieAsync(HttpClient client, string path)
        {
            using (HttpResponseMessage response = await client.GetAsync(path))
            {
                Program.Assert(response.IsSuccessStatusCode, "ASP.NET Core cookie issuance failed.");
                return GetSetCookieHeaders(response);
            }
        }

        private static string[] GetSetCookieHeaders(HttpResponseMessage response)
        {
            return response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> values)
                ? values.ToArray()
                : Array.Empty<string>();
        }

        private static string GetCookieValue(IEnumerable<string> setCookieHeaders, string cookieName)
        {
            string prefix = cookieName + "=";
            string header = setCookieHeaders.Single(value => value.StartsWith(prefix, StringComparison.Ordinal));
            string pair = header.Split(';')[0];
            return pair.Substring(prefix.Length);
        }

        private static void AssertCookiePath(IEnumerable<string> setCookieHeaders)
        {
            Program.Assert(
                setCookieHeaders.All(header => header.IndexOf("path=" + SharedCookieConstants.CookiePath, StringComparison.OrdinalIgnoreCase) >= 0),
                "ASP.NET Core did not emit the shared cookie path.");
        }
    }
}
