// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Moq;
using NuGetGallery.Authentication;

namespace NuGetGallery.Framework
{
    public static class Fakes
    {
        private static readonly MethodInfo SetMethod = typeof(IEntitiesContext).GetMethod("Set");

        public static readonly string Password = "p@ssw0rd!";

        public static readonly User User = new User("testUser") { 
            Key = 42,
            EmailAddress = "confirmed1@example.com",
            Credentials = new List<Credential>() { 
                CredentialBuilder.CreatePbkdf2Password(Password),
                CredentialBuilder.CreateV1ApiKey(Guid.Parse("519e180e-335c-491a-ac26-e83c4bd31d65"))
            }
        };

        public static readonly User ShaUser = new User("testShaUser")
        {
            Key = 42,
            EmailAddress = "confirmed2@example.com",
            Credentials = new List<Credential>() { 
                CredentialBuilder.CreateSha1Password(Password),
                CredentialBuilder.CreateV1ApiKey(Guid.Parse("b9704a41-4107-4cd2-bcfa-70d84e021ab2"))
            }
        };
        public static readonly User Admin = new User("testAdmin") { 
            Key = 43,
            EmailAddress = "confirmed3@example.com",
            Credentials = new List<Credential>() { CredentialBuilder.CreatePbkdf2Password(Password) }, 
            Roles = new List<Role>() { new Role() { Name = Constants.AdminRoleName } } 
        };
        public static readonly User Owner = new User("testPackageOwner") { 
            Key = 44,
            Credentials = new List<Credential>() { CredentialBuilder.CreatePbkdf2Password(Password) },
            EmailAddress = "confirmed@example.com" //package owners need confirmed email addresses, obviously.
        };

        public static readonly PackageRegistration Package = new PackageRegistration()
        {
            Id = "FakePackage",
            Owners = new List<User>() { Owner },
            Packages = new List<Package>() {
                new Package() { Version = "1.0" },
                new Package() { Version = "2.0" }
            }
        };

        public static User CreateUser(string userName, params Credential[] credentials)
        {
            return new User(userName)
            {
                UnconfirmedEmailAddress = "un@confirmed.com",
                Credentials = new List<Credential>(credentials)
            };
        }

        public static ClaimsPrincipal ToPrincipal(this User user)
        {
            ClaimsIdentity identity = new ClaimsIdentity(
                claims: Enumerable.Concat(new[] {
                            new Claim(ClaimsIdentity.DefaultNameClaimType, user.Username),
                        }, user.Roles.Select(r => new Claim(ClaimsIdentity.DefaultRoleClaimType, r.Name))),
                authenticationType: "Test",
                nameType: ClaimsIdentity.DefaultNameClaimType,
                roleType: ClaimsIdentity.DefaultRoleClaimType);
            return new ClaimsPrincipal(identity);
        }

        public static IIdentity ToIdentity(this User user)
        {
             return new GenericIdentity(user.Username);
        }

        internal static void ConfigureEntitiesContext(FakeEntitiesContext ctxt)
        {
            // Add Users
            var users = ctxt.Set<User>();
            users.Add(User);
            users.Add(ShaUser);
            users.Add(Admin);
            users.Add(Owner);

            // Add Credentials and link to users
            var creds = ctxt.Set<Credential>();
            foreach (var user in users)
            {
                foreach (var cred in user.Credentials)
                {
                    cred.User = user;
                    creds.Add(cred);
                }
            }
        }

        public static IOwinContext CreateOwinContext()
        {
            var ctx = new OwinContext();

            ctx.Request.SetUrl("http://nuget.local/");

            // Fill in some values that cause exceptions if not present
            ctx.Set<Action<Action<object>, object>>("server.OnSendingHeaders", (_, __) => { });

            return ctx;
        }

        public static Mock<OwinMiddleware> CreateOwinMiddleware()
        {
            var middleware = new Mock<OwinMiddleware>(new object[] { null });
            middleware.Setup(m => m.Invoke(It.IsAny<OwinContext>())).Completes();
            return middleware;
        }
    }
}
