// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using Microsoft.Owin;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Authentication;

namespace NuGetGallery.Framework
{
    public class Fakes
    {
        public static TimeSpan ExpirationForApiKeyV1 = TimeSpan.FromDays(90);

        public static readonly string Password = "p@ssw0rd!";

        public Fakes()
        {
            _users = Get<User>();
            _packageRegistrations = Get<PackageRegistration>();

            var key = 39;
            var credentialBuilder = new CredentialBuilder();

            ApiKeyV3PlaintextValue = "889e180e-335c-491a-ac26-e83c4bd31d87";

            User = new User("testUser") // NOTE: Do not change the casing of this username. It will break tests for the ChangeUsername in the Admin Panel
            {
                Key = key++,
                EmailAddress = "confirmed0@example.com",
                Credentials = new List<Credential>
                {
                    credentialBuilder.CreatePasswordCredential(Password),
                    TestCredentialHelper.CreateV1ApiKey(Guid.Parse("669e180e-335c-491a-ac26-e83c4bd31d65"),
                        ExpirationForApiKeyV1),
                    TestCredentialHelper.CreateV2ApiKey(Guid.Parse("779e180e-335c-491a-ac26-e83c4bd31d87"),
                        ExpirationForApiKeyV1).WithDefaultScopes(),
                    TestCredentialHelper.CreateV3ApiKey(Guid.Parse(ApiKeyV3PlaintextValue),
                        ExpirationForApiKeyV1).WithDefaultScopes(),
                    TestCredentialHelper.CreateV4ApiKey(null, out string apiKeyV4PlaintextValue).WithDefaultScopes(),
                    TestCredentialHelper.CreateV2VerificationApiKey(Guid.Parse("b0c51551-823f-4701-8496-43980b4b3913")),
                    TestCredentialHelper.CreateExternalMSACredential("abc"),
                    TestCredentialHelper.CreateExternalAADCredential("def", "tenant1")
                },
                CreatedUtc = new DateTime(2018, 4, 1)
            };
            foreach (var c in User.Credentials)
            {
                c.User = User;
            }

            ApiKeyV4PlaintextValue = apiKeyV4PlaintextValue;

            Organization = new Organization("testOrganization") // NOTE: Do not change the casing of this username. It will break tests for the ChangeUsername in the Admin Panel
            {
                Key = key++,
                EmailAddress = "confirmedOrganization@example.com",
                // invalid credentials for testing authentication constraints
                Credentials = new List<Credential>
                {
                    credentialBuilder.CreatePasswordCredential(Password)
                },
                MemberRequests = new List<MembershipRequest>()
            };

            CreateOrganizationUsers(ref key, credentialBuilder, "", out var organization, out var organizationAdmin, out var organizationCollaborator);
            Organization = organization;
            OrganizationAdmin = organizationAdmin;
            OrganizationCollaborator = organizationCollaborator;

            CreateOrganizationUsers(ref key, credentialBuilder, "Owner", out var organizationOwner, out var organizationAdminOwner, out var organizationCollaboratorOwner);
            OrganizationOwner = organizationOwner;
            OrganizationOwnerAdmin = organizationAdminOwner;
            OrganizationOwnerCollaborator = organizationCollaboratorOwner;

            Pbkdf2User = new User("testPbkdf2User")
            {
                Key = key++,
                EmailAddress = "confirmed1@example.com",
                Credentials = new List<Credential>
                {
                    TestCredentialHelper.CreatePbkdf2Password(Password),
                    TestCredentialHelper.CreateV1ApiKey(Guid.Parse("519e180e-335c-491a-ac26-e83c4bd31d65"),
                        ExpirationForApiKeyV1)
                }
            };

            ShaUser = new User("testShaUser")
            {
                Key = key++,
                EmailAddress = "confirmed2@example.com",
                Credentials = new List<Credential>
                {
                    TestCredentialHelper.CreateSha1Password(Password),
                    TestCredentialHelper.CreateV1ApiKey(Guid.Parse("b9704a41-4107-4cd2-bcfa-70d84e021ab2"),
                        ExpirationForApiKeyV1)
                }
            };

            Admin = new User("testAdmin")
            {
                Key = key++,
                EmailAddress = "confirmed3@example.com",
                Credentials = new List<Credential>
                {
                    TestCredentialHelper.CreatePbkdf2Password(Password)
                },
                Roles = new List<Role>
                {
                    new Role {Name = CoreConstants.AdminRoleName}
                }
            };

            Owner = new User("testPackageOwner")
            {
                Key = key++,
                Credentials = new List<Credential> { TestCredentialHelper.CreatePbkdf2Password(Password) },
                EmailAddress = "confirmed@example.com" //package owners need confirmed email addresses, obviously.
            };

            Package = new PackageRegistration
            {
                Id = "FakePackage",
                Owners = new List<User> { Owner, OrganizationOwner },
            };
            Package.Packages = new List<Package>
            {
                new Package { Version = "1.0", PackageRegistration = Package },
                new Package { Version = "2.0", PackageRegistration = Package }
            };
        }

        public User User { get; }

        public Organization Organization { get; }

        public User OrganizationAdmin { get; }

        public User OrganizationCollaborator { get; }

        public Organization OrganizationOwner { get; }

        public User OrganizationOwnerAdmin { get; }

        public User OrganizationOwnerCollaborator { get; }

        public User ShaUser { get; }

        public User Pbkdf2User { get; }

        public User Admin { get; }

        public User Owner { get; }

        public PackageRegistration Package { get; }

        public string ApiKeyV3PlaintextValue { get; }
        public string ApiKeyV4PlaintextValue { get; }

        public User CreateUser(string userName, params Credential[] credentials)
        {
            var user = new User(userName)
            {
                UnconfirmedEmailAddress = "un@confirmed.com",
                Credentials = new List<Credential>(credentials)
            };
            foreach (var credential in credentials)
            {
                credential.User = user;
            }
            return user;
        }

        public static ClaimsPrincipal ToPrincipal(User user)
        {
            if (user == null)
            {
                return null;
            }

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
            // Add Users and Credentials
            var users = ctxt.Set<User>();
            var creds = ctxt.Set<Credential>();
            foreach (var user in Users)
            {
                users.Add(user);
                foreach (var cred in user.Credentials)
                {
                    cred.User = user;
                    creds.Add(cred);
                }
            }

            // Add Packages and Registrations
            var packageRegistrations = ctxt.Set<PackageRegistration>();
            var packages = ctxt.Set<Package>();
            foreach (var packageRegistration in PackageRegistrations)
            {
                packageRegistrations.Add(packageRegistration);
                foreach (var package in packageRegistration.Packages)
                {
                    packages.Add(package);
                }
            }
        }

        public static IOwinContext CreateOwinContext()
        {
            var ctx = new OwinContext();

            ctx.Request.SetUrl(TestUtility.GallerySiteRootHttps);

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

        private IEnumerable<Func<User>> _users;
        public IEnumerable<User> Users => _users.Select(f => f());

        private IEnumerable<Func<PackageRegistration>> _packageRegistrations;
        public IEnumerable<PackageRegistration> PackageRegistrations => _packageRegistrations.Select(f => f());

        private IEnumerable<Func<T>> Get<T>()
        {
            return typeof(Fakes)
                .GetProperties()
                .Where(p => typeof(T).IsAssignableFrom(p.PropertyType) && p.GetMethod != null)
                .Select<PropertyInfo, Func<T>>(p =>
                    () => (T)p.GetMethod.Invoke(this, new object[] { }));
        }

        private void CreateOrganizationUsers(ref int key, CredentialBuilder credentialBuilder, string suffix, out Organization organization, out User admin, out User collaborator)
        {
            organization = new Organization("testOrganization" + suffix)
            {
                Key = key++,
                EmailAddress = $"confirmedOrganization{suffix}@example.com",
                // invalid credentials for testing authentication constraints
                Credentials = new List<Credential>
                {
                    credentialBuilder.CreatePasswordCredential(Password)
                },
                MemberRequests = new List<MembershipRequest>()
            };

            admin = new User("testOrganizationAdmin" + suffix)
            {
                Key = key++,
                EmailAddress = $"confirmedOrganizationAdmin{suffix}@example.com",
                Credentials = new List<Credential>
                {
                    credentialBuilder.CreatePasswordCredential(Password)
                }
            };

            var adminMembership = new Membership
            {
                Organization = organization,
                Member = admin,
                IsAdmin = true
            };
            admin.Organizations.Add(adminMembership);
            organization.Members.Add(adminMembership);

            collaborator = new User("testOrganizationCollaborator" + suffix)
            {
                Key = key++,
                EmailAddress = $"confirmedOrganizationCollaborator{suffix}@example.com",
                Credentials = new List<Credential>
                {
                    credentialBuilder.CreatePasswordCredential(Password)
                }
            };

            var collaboratorMembership = new Membership
            {
                Organization = organization,
                Member = collaborator,
                IsAdmin = false
            };
            collaborator.Organizations.Add(collaboratorMembership);
            organization.Members.Add(collaboratorMembership);

            organization.Members = admin.Organizations.Concat(collaborator.Organizations).ToList();
        }
    }
}
