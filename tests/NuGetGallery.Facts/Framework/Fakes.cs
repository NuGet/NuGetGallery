// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Microsoft.Owin;
using Moq;
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Authentication;

namespace NuGetGallery.Framework
{
    public class Fakes
    {
        public static TimeSpan ExpirationForApiKeyV1 =  TimeSpan.FromDays(90);

        public static readonly string Password = "p@ssw0rd!";
        
        public Fakes()
        {
            User = new User("testUser")
            {
                Key = 40,
                EmailAddress = "confirmed0@example.com",
                Credentials = new List<Credential>
                {
                    new CredentialBuilder().CreatePasswordCredential(Password),
                    TestCredentialBuilder.CreateV1ApiKey(Guid.Parse("669e180e-335c-491a-ac26-e83c4bd31d65"),
                        ExpirationForApiKeyV1)
                }
            };

            Pbkdf2User = new User("testPbkdf2User")
            {
                Key = 41,
                EmailAddress = "confirmed1@example.com",
                Credentials = new List<Credential>
                {
                    TestCredentialBuilder.CreatePbkdf2Password(Password),
                    TestCredentialBuilder.CreateV1ApiKey(Guid.Parse("519e180e-335c-491a-ac26-e83c4bd31d65"),
                        ExpirationForApiKeyV1)
                }
            };

            ShaUser = new User("testShaUser")
            {
                Key = 42,
                EmailAddress = "confirmed2@example.com",
                Credentials = new List<Credential>
                {
                    TestCredentialBuilder.CreateSha1Password(Password),
                    TestCredentialBuilder.CreateV1ApiKey(Guid.Parse("b9704a41-4107-4cd2-bcfa-70d84e021ab2"),
                        ExpirationForApiKeyV1)
                }
            };

            Admin = new User("testAdmin")
            {
                Key = 43,
                EmailAddress = "confirmed3@example.com",
                Credentials = new List<Credential> { TestCredentialBuilder.CreatePbkdf2Password(Password)},
                Roles = new List<Role> {new Role {Name = Constants.AdminRoleName}}
            };

            Owner = new User("testPackageOwner")
            {
                Key = 44,
                Credentials = new List<Credential> { TestCredentialBuilder.CreatePbkdf2Password(Password)},
                EmailAddress = "confirmed@example.com" //package owners need confirmed email addresses, obviously.
            };

            Package = new PackageRegistration
            {
                Id = "FakePackage",
                Owners = new List<User> {Owner},
                Packages = new List<Package>
                {
                    new Package {Version = "1.0"},
                    new Package {Version = "2.0"}
                }
            };
        }

        public User User { get; }

        public User ShaUser { get; }

        public User Pbkdf2User { get; }

        public User Admin { get; }

        public User Owner { get; }

        public PackageRegistration Package { get; }

        public User CreateUser(string userName, params Credential[] credentials)
        {
            return new User(userName)
            {
                UnconfirmedEmailAddress = "un@confirmed.com",
                Credentials = new List<Credential>(credentials)
            };
        }

        public static ClaimsPrincipal ToPrincipal(User user)
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

        public static IIdentity ToIdentity(User user)
        {
             return new GenericIdentity(user.Username);
        }

        internal void ConfigureEntitiesContext(FakeEntitiesContext ctxt)
        {
            // Add Users
            var users = ctxt.Set<User>();
            users.Add(User);
            users.Add(Pbkdf2User);
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
