// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using NuGetGallery.Authentication;

namespace NuGetGallery.CookieInteropTests
{
    internal static class Program
    {
        private const string Username = "interop-user";
        private static readonly string[] Roles = { "Administrators", "PackageOwners" };

        private static async Task<int> Main(string[] args)
        {
            try
            {
                if (args.Length != 2)
                {
                    throw new ArgumentException("Expected: <operation> <artifact-directory>");
                }

                Directory.CreateDirectory(args[1]);

#if NET472
                if (args[0] == "legacy-issue")
                {
                    await LegacyCookieRunner.IssueAsync(args[1]);
                }
                else if (args[0] == "legacy-accept")
                {
                    await LegacyCookieRunner.AcceptAsync(args[1]);
                }
                else
                {
                    throw new ArgumentException("The net472 harness supports legacy-issue and legacy-accept.");
                }
#else
                if (args[0] == "core-exchange")
                {
                    await CoreCookieRunner.ExchangeAsync(args[1]);
                }
                else
                {
                    throw new ArgumentException("The net10.0 harness supports core-exchange.");
                }
#endif

                Console.WriteLine($"{args[0]} passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        internal static ClaimsIdentity CreateIdentity(bool oversized = false)
        {
            var claims = new List<Claim>
            {
                new Claim(SharedCookieConstants.NameClaimType, Username),
                new Claim(SharedCookieConstants.NameIdentifierClaimType, Username),
                new Claim(SharedCookieConstants.AuthenticationMethodClaimType, SharedCookieConstants.AuthenticationScheme),
                new Claim(SharedCookieConstants.DiscontinuedLoginClaimType, bool.TrueString),
                new Claim(SharedCookieConstants.PasswordLoginClaimType, bool.TrueString),
                new Claim(SharedCookieConstants.ExternalLoginClaimType, bool.TrueString),
                new Claim(SharedCookieConstants.ExternalCredentialIdentitiesClaimType, "interop@example.test"),
                new Claim(SharedCookieConstants.EnabledMultiFactorAuthenticationClaimType, bool.TrueString),
                new Claim(SharedCookieConstants.WasMultiFactorAuthenticatedClaimType, bool.TrueString),
                new Claim(SharedCookieConstants.ExternalLoginCredentialTypeClaimType, NuGetClaims.ExternalLoginCredentialValues.AzureActiveDirectory),
            };

            claims.AddRange(Roles.Select(role => new Claim(SharedCookieConstants.RoleClaimType, role)));
            if (oversized)
            {
                claims.Add(new Claim("https://claims.nuget.org/interop-payload", new string('x', 20000)));
            }

            return new ClaimsIdentity(
                claims,
                SharedCookieConstants.AuthenticationScheme,
                SharedCookieConstants.NameClaimType,
                SharedCookieConstants.RoleClaimType);
        }

        internal static void AssertPrincipal(ClaimsPrincipal principal)
        {
            Assert(principal?.Identity?.IsAuthenticated == true, "The principal was not authenticated.");
            Assert(principal.Identity.AuthenticationType == SharedCookieConstants.AuthenticationScheme, "The authentication type changed.");
            Assert(principal.Identity.Name == Username, "The username changed.");
            Assert(FindValue(principal, SharedCookieConstants.NameIdentifierClaimType) == Username, "The name identifier changed.");
            Assert(FindValue(principal, SharedCookieConstants.AuthenticationMethodClaimType) == SharedCookieConstants.AuthenticationScheme, "The authentication method changed.");

            foreach (var role in Roles)
            {
                Assert(principal.IsInRole(role), $"Role '{role}' was not preserved.");
            }

            AssertClaim(principal, SharedCookieConstants.DiscontinuedLoginClaimType, bool.TrueString);
            AssertClaim(principal, SharedCookieConstants.PasswordLoginClaimType, bool.TrueString);
            AssertClaim(principal, SharedCookieConstants.ExternalLoginClaimType, bool.TrueString);
            AssertClaim(principal, SharedCookieConstants.ExternalCredentialIdentitiesClaimType, "interop@example.test");
            AssertClaim(principal, SharedCookieConstants.EnabledMultiFactorAuthenticationClaimType, bool.TrueString);
            AssertClaim(principal, SharedCookieConstants.WasMultiFactorAuthenticatedClaimType, bool.TrueString);
            AssertClaim(principal, SharedCookieConstants.ExternalLoginCredentialTypeClaimType, NuGetClaims.ExternalLoginCredentialValues.AzureActiveDirectory);
        }

        internal static string ToCookieHeader(IEnumerable<string> setCookieHeaders)
        {
            return string.Join("; ", setCookieHeaders.Select(header => header.Split(';')[0]));
        }

        internal static string Read(string artifactDirectory, string name)
        {
            return File.ReadAllText(Path.Combine(artifactDirectory, name));
        }

        internal static string[] ReadLines(string artifactDirectory, string name)
        {
            return File.ReadAllLines(Path.Combine(artifactDirectory, name));
        }

        internal static void Write(string artifactDirectory, string name, string value)
        {
            File.WriteAllText(Path.Combine(artifactDirectory, name), value);
        }

        internal static void WriteLines(string artifactDirectory, string name, IEnumerable<string> values)
        {
            File.WriteAllLines(Path.Combine(artifactDirectory, name), values);
        }

        internal static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertClaim(ClaimsPrincipal principal, string type, string value)
        {
            Assert(FindValue(principal, type) == value, $"Claim '{type}' changed.");
        }

        private static string FindValue(ClaimsPrincipal principal, string type)
        {
            return principal.Claims.Single(claim => claim.Type == type).Value;
        }
    }
}
