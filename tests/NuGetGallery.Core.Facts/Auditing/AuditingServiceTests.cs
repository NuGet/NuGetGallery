// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Auditing.AuditedEntities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class AuditingServiceTests
    {
        [Fact]
        public async Task SaveAuditRecordAsync_UserAuditRecord()
        {
            var user = new User()
            {
                CreatedUtc = DateTime.Now,
                Credentials = new List<Credential>()
                {
                    new Credential(
                        CredentialTypes.Password.V3,
                        value: "a",
                        expiration: new TimeSpan(days: 1, hours: 2, minutes:3, seconds: 4))
                },
                EmailAddress = "b",
                Roles = new List<Role>() { new Role() { Key = 5, Name = "c" } },
                UnconfirmedEmailAddress = "d",
                Username = "e"
            };
            var auditRecord = new UserAuditRecord(user, AuditedUserAction.Login, user.Credentials.First());
            var service = new TestAuditingService(async (string auditData, string resourceType, string filePath, string action, DateTime timestamp) =>
            {
                Assert.Equal("User", resourceType);
                Assert.Equal("e", filePath);
                Assert.Equal("login", action);
                Assert.InRange(timestamp, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));

                var jObject = JObject.Parse(auditData);

                var record = jObject["Record"];

                Assert.Equal("e", record["Username"].Value<string>());
                Assert.Equal("b", record["EmailAddress"].Value<string>());
                Assert.Equal("d", record["UnconfirmedEmailAddress"].Value<string>());
                Assert.Equal("c", record["Roles"].ToObject<IList<string>>().Single());

                var credentials = record["Credentials"];
                var credential = credentials.AsEnumerable().Single();

                Assert.Equal(0, credential["Key"].Value<int>());
                Assert.Equal(CredentialTypes.Password.V3, credential["Type"].Value<string>());
                Assert.Equal(JTokenType.Null, credential["Value"].Type);
                Assert.Equal(JTokenType.Null, credential["Description"].Type);
                Assert.False(credential["Scopes"].ToObject<IList<object>>().Any());
                Assert.Equal(JTokenType.Null, credential["Identity"].Type);
                Assert.Equal(DateTime.MinValue, credential["Created"].Value<DateTime>());
                Assert.Equal(user.Credentials.First().Expires.Value, credential["Expires"].Value<DateTime>());
                Assert.Equal(JTokenType.Null, credential["LastUsed"].Type);

                var affectedCredential = record["AffectedCredential"].AsJEnumerable().Single();

                Assert.Equal(0, affectedCredential["Key"].Value<int>());
                Assert.Equal(CredentialTypes.Password.V3, affectedCredential["Type"].Value<string>());
                Assert.Equal(JTokenType.Null, affectedCredential["Value"].Type);
                Assert.Equal(JTokenType.Null, affectedCredential["Description"].Type);
                Assert.Empty(affectedCredential["Scopes"].AsJEnumerable());
                Assert.Equal(JTokenType.Null, affectedCredential["Identity"].Type);
                Assert.Equal(DateTime.MinValue, affectedCredential["Created"].Value<DateTime>());
                Assert.Equal(user.Credentials.First().Expires.Value, affectedCredential["Expires"].Value<DateTime>());
                Assert.Equal(JTokenType.Null, affectedCredential["LastUsed"].Type);

                Assert.Equal(JTokenType.Null, record["AffectedEmailAddress"].Type);
                Assert.Equal("Login", record["Action"].Value<string>());

                await VerifyActor(jObject);

                return null;
            });

            await service.SaveAuditRecordAsync(auditRecord);
        }

        [Fact]
        public async Task SaveAuditRecordAsync_PackageAuditRecord()
        {
            var package = new Package()
            {
                Copyright = "a",
                Created = DateTime.Now,
#pragma warning disable CS0612 // Type or member is obsolete
                Deleted = true,
#pragma warning restore CS0612 // Type or member is obsolete
                Description = "b",
                DownloadCount = 1,
#pragma warning disable CS0612 // Type or member is obsolete
                ExternalPackageUrl = "c",
#pragma warning restore CS0612 // Type or member is obsolete
                FlattenedAuthors = "d",
                FlattenedDependencies = "e",
                Hash = "f",
                HashAlgorithm = "g",
                HideLicenseReport = true,
                IconUrl = "h",
                IsLatest = true,
                IsLatestStable = true,
                IsPrerelease = true,
                Key = 2,
                Language = "i",
                LastEdited = DateTime.Now.AddMinutes(1),
                LastUpdated = DateTime.Now.AddMinutes(2),
                LicenseNames = "j",
                LicenseReportUrl = "k",
                LicenseUrl = "l",
                Listed = true,
                MinClientVersion = "m",
                NormalizedVersion = "n",
                PackageFileSize = 3,
                PackageRegistration = new PackageRegistration() { Id = "o" },
                PackageRegistrationKey = 4,
                PackageStatusKey = PackageStatus.Deleted,
                ProjectUrl = "p",
                Published = DateTime.Now.AddMinutes(3),
                ReleaseNotes = "q",
                RequiresLicenseAcceptance = true,
                DevelopmentDependency = true,
                Summary = "r",
                Tags = "s",
                Title = "t",
                UserKey = 5,
                Version = "u"
            };
            var auditRecord = new PackageAuditRecord(package, AuditedPackageAction.Create, reason: "v");
            var service = new TestAuditingService(async (string auditData, string resourceType, string filePath, string action, DateTime timestamp) =>
            {
                Assert.Equal("Package", resourceType);
                Assert.Equal("o/u", filePath);
                Assert.Equal("create", action);
                Assert.InRange(timestamp, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));

                var jObject = JObject.Parse(auditData);

                var record = jObject["Record"];

                Assert.Equal("o", record["Id"].Value<string>());
                Assert.Equal("u", record["Version"].Value<string>());
                Assert.Equal("f", record["Hash"].Value<string>());

                var packageRecord = record["PackageRecord"];

                Assert.Equal(4, packageRecord["PackageRegistrationKey"].Value<int>());
                Assert.Equal("a", packageRecord["Copyright"].Value<string>());
                Assert.Equal(package.Created.ToUniversalTime(), packageRecord["Created"].Value<DateTime>());
                Assert.Equal("b", packageRecord["Description"].Value<string>());
                Assert.Equal("q", packageRecord["ReleaseNotes"].Value<string>());
                Assert.Equal(1, packageRecord["DownloadCount"].Value<int>());
                Assert.Equal(JTokenType.Null, packageRecord["ExternalPackageUrl"].Type);
                Assert.Equal("g", packageRecord["HashAlgorithm"].Value<string>());
                Assert.Equal("f", packageRecord["Hash"].Value<string>());
                Assert.Equal("h", packageRecord["IconUrl"].Value<string>());
                Assert.True(packageRecord["IsLatest"].Value<bool>());
                Assert.True(packageRecord["IsLatestStable"].Value<bool>());
                Assert.Equal(package.LastUpdated.ToUniversalTime(), packageRecord["LastUpdated"].Value<DateTime>());
                Assert.Equal(package.LastEdited.Value.ToUniversalTime(), packageRecord["LastEdited"].Value<DateTime>());
                Assert.Equal("l", packageRecord["LicenseUrl"].Value<string>());
                Assert.True(packageRecord["HideLicenseReport"].Value<bool>());
                Assert.Equal("i", packageRecord["Language"].Value<string>());
                Assert.Equal(package.Published.ToUniversalTime(), packageRecord["Published"].Value<DateTime>());
                Assert.Equal(3, packageRecord["PackageFileSize"].Value<int>());
                Assert.Equal("p", packageRecord["ProjectUrl"].Value<string>());
                Assert.True(packageRecord["RequiresLicenseAcceptance"].Value<bool>());
                Assert.True(packageRecord["DevelopmentDependency"].Value<bool>());
                Assert.Equal("r", packageRecord["Summary"].Value<string>());
                Assert.Equal("s", packageRecord["Tags"].Value<string>());
                Assert.Equal("t", packageRecord["Title"].Value<string>());
                Assert.Equal("u", packageRecord["Version"].Value<string>());
                Assert.Equal("n", packageRecord["NormalizedVersion"].Value<string>());
                Assert.Equal("j", packageRecord["LicenseNames"].Value<string>());
                Assert.Equal("k", packageRecord["LicenseReportUrl"].Value<string>());
                Assert.True(packageRecord["Listed"].Value<bool>());
                Assert.True(packageRecord["IsPrerelease"].Value<bool>());
                Assert.Equal("d", packageRecord["FlattenedAuthors"].Value<string>());
                Assert.Equal("e", packageRecord["FlattenedDependencies"].Value<string>());
                Assert.Equal(2, packageRecord["Key"].Value<int>());
                Assert.Equal("m", packageRecord["MinClientVersion"].Value<string>());
                Assert.Equal(5, packageRecord["UserKey"].Value<int>());
                Assert.True(packageRecord["Deleted"].Value<bool>());
                Assert.Equal(1, packageRecord["PackageStatusKey"].Value<int>());

                var registrationRecord = record["RegistrationRecord"];

                Assert.Equal("o", registrationRecord["Id"].Value<string>());
                Assert.Equal(0, registrationRecord["DownloadCount"].Value<int>());
                Assert.Equal(0, registrationRecord["Key"].Value<int>());

                Assert.Equal("v", record["Reason"].Value<string>());
                Assert.Equal("Create", record["Action"].Value<string>());

                await VerifyActor(jObject);

                return null;
            });

            await service.SaveAuditRecordAsync(auditRecord);
        }

        [Fact]
        public async Task SaveAuditRecordAsync_PackageRegistrationAuditRecord()
        {
            var packageRegistration = new PackageRegistration()
            {
                DownloadCount = 1,
                Id = "a",
                Key = 2
            };
            var auditRecord = new PackageRegistrationAuditRecord(packageRegistration, AuditedPackageRegistrationAction.AddOwner, owner: "b");
            var service = new TestAuditingService(async (string auditData, string resourceType, string filePath, string action, DateTime timestamp) =>
            {
                Assert.Equal("PackageRegistration", resourceType);
                Assert.Equal("a", filePath);
                Assert.Equal("addowner", action);
                Assert.InRange(timestamp, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));

                var jObject = JObject.Parse(auditData);

                var record = jObject["Record"];

                Assert.Equal("a", record["Id"].Value<string>());

                var registrationRecord = record["RegistrationRecord"];

                Assert.Equal("a", registrationRecord["Id"].Value<string>());
                Assert.Equal(1, registrationRecord["DownloadCount"].Value<int>());
                Assert.Equal(2, registrationRecord["Key"].Value<int>());

                Assert.Equal("b", record["Owner"].Value<string>());
                Assert.Equal("AddOwner", record["Action"].Value<string>());

                await VerifyActor(jObject);

                return null;
            });

            await service.SaveAuditRecordAsync(auditRecord);
        }

        [Fact]
        public async Task SaveAuditRecordAsync_FailedAuthenticatedOperationAuditRecord()
        {
            var expiresIn = new TimeSpan(days: 1, hours: 2, minutes: 3, seconds: 4);
            var auditRecord = new FailedAuthenticatedOperationAuditRecord(
                usernameOrEmail: "a",
                action: AuditedAuthenticatedOperationAction.PackagePushAttemptByNonOwner,
                attemptedPackage: new AuditedPackageIdentifier("b", "c"),
                attemptedCredential: new Credential(CredentialTypes.ApiKey.V2, value: "d", expiration: expiresIn));
            var service = new TestAuditingService(async (string auditData, string resourceType, string filePath, string action, DateTime timestamp) =>
            {
                Assert.Equal("FailedAuthenticatedOperation", resourceType);
                Assert.Equal("all", filePath);
                Assert.Equal("packagepushattemptbynonowner", action);
                Assert.InRange(timestamp, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));

                var jObject = JObject.Parse(auditData);

                var record = jObject["Record"];

                Assert.Equal("a", record["UsernameOrEmail"].Value<string>());

                var attemptedPackage = record["AttemptedPackage"];

                Assert.Equal("b", attemptedPackage["Id"].Value<string>());
                Assert.Equal("c", attemptedPackage["Version"].Value<string>());

                var attemptedCredential = record["AttemptedCredential"];

                Assert.Equal(0, attemptedCredential["Key"].Value<int>());
                Assert.Equal(CredentialTypes.ApiKey.V2, attemptedCredential["Type"].Value<string>());

                Assert.Equal(JTokenType.Null, attemptedCredential["Value"].Type);
                Assert.Equal(JTokenType.Null, attemptedCredential["Description"].Type);
                Assert.False(attemptedCredential["Scopes"].ToObject<IList<object>>().Any());
                Assert.Equal(JTokenType.Null, attemptedCredential["Identity"].Type);
                Assert.Equal(DateTime.MinValue, attemptedCredential["Created"].Value<DateTime>());

                var expiresUtc = DateTime.UtcNow.Add(expiresIn);

                Assert.InRange(attemptedCredential["Expires"].Value<DateTime>(), expiresUtc.AddMinutes(-1), expiresUtc.AddMinutes(1));
                Assert.Equal(JTokenType.Null, attemptedCredential["LastUsed"].Type);

                await VerifyActor(jObject);

                return null;
            });

            await service.SaveAuditRecordAsync(auditRecord);
        }

        private static async Task VerifyActor(JObject jObject)
        {
            var actor = jObject["Actor"];

            Assert.Equal(Environment.MachineName, actor["MachineName"].Value<string>());

            var expectedIpAddress = await AuditActor.GetLocalIpAddressAsync();

            Assert.Equal(expectedIpAddress, actor["MachineIP"].Value<string>());
            Assert.Equal($@"{Environment.UserDomainName}\{Environment.UserName}", actor["UserName"].Value<string>());
            Assert.Equal("MachineUser", actor["AuthenticationType"].Value<string>());
            Assert.InRange(actor["TimestampUtc"].Value<DateTime>(), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            Assert.Equal(JTokenType.Null, actor["OnBehalfOf"].Type);
        }

        private class TestAuditingService : AuditingService
        {
            private readonly Func<string, string, string, string, DateTime, Task<Uri>> _saveDelegate;

            internal TestAuditingService(Func<string, string, string, string, DateTime, Task<Uri>> saveDelegate)
            {
                _saveDelegate = saveDelegate;
            }

            protected override Task SaveAuditRecordAsync(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
            {
                return _saveDelegate(auditData, resourceType, filePath, action, timestamp);
            }
        }
    }
}