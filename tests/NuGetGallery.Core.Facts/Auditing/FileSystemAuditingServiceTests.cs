// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Utilities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class FileSystemAuditingServiceTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_ThrowsForNullOrEmptyAuditingPath(string auditingPath)
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FileSystemAuditingService(auditingPath, GetOnBehalfOf));
        }

        [Fact]
        public void Constructor_ThrowsForNullGetOnBehalfOf()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FileSystemAuditingService(auditingPath: "a", getOnBehalfOf: null));
        }

        [Fact]
        public async Task SaveAuditRecord_ThrowsForNull()
        {
            var service = new FileSystemAuditingService(
                auditingPath: "a",
                getOnBehalfOf: AuditActor.GetAspNetOnBehalfOfAsync);

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await service.SaveAuditRecordAsync(record: null));
        }

        [Fact]
        public async Task SaveAuditRecord_ReturnsUriForAuditFile()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var service = new FileSystemAuditingService(
                    auditingPath: testDirectory.FullPath,
                    getOnBehalfOf: GetOnBehalfOf);
                var record = new PackageAuditRecord(
                    new Package()
                    {
                        Hash = "a",
                        PackageRegistration = new PackageRegistration() { Id = "b" },
                        Version = "1.0.0"
                    },
                    AuditedPackageAction.Create);

                await service.SaveAuditRecordAsync(record);

                var files = Directory.GetFiles(testDirectory.FullPath, "*", SearchOption.AllDirectories);
                var actualFilePath = files.Single();
                var expectedFilePathPattern = new Regex(@"package\\b\\1.0.0\\[0-9a-f]{32}-create.audit.v1.json$");

                Assert.Matches(expectedFilePathPattern, actualFilePath);
            }
        }

        [Fact]
        public async Task SaveAuditRecord_CreatesAuditFile()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var service = new FileSystemAuditingService(
                    auditingPath: testDirectory.FullPath,
                    getOnBehalfOf: GetOnBehalfOf);
                var record = new PackageAuditRecord(
                    new Package()
                    {
                        Hash = "a",
                        PackageRegistration = new PackageRegistration() { Id = "b" },
                        Version = "1.0.0"
                    },
                    AuditedPackageAction.Create);

                await service.SaveAuditRecordAsync(record);

                var files = Directory.GetFiles(testDirectory.FullPath, "*", SearchOption.AllDirectories);
                var json = JObject.Parse(File.ReadAllText(files.Single()));

                Assert.NotNull(json["Record"]);
                Assert.NotNull(json["Record"]["Id"]);
                Assert.Equal("b", json["Record"]["Id"].Value<string>());
                Assert.NotNull(json["Record"]["Version"]);
                Assert.Equal("1.0.0", json["Record"]["Version"].Value<string>());
                Assert.NotNull(json["Record"]["Hash"]);
                Assert.Equal("a", json["Record"]["Hash"].Value<string>());
                Assert.NotNull(json["Record"]["PackageRecord"]);
                Assert.NotNull(json["Record"]["PackageRecord"]["Hash"]);
                Assert.Equal("a", json["Record"]["PackageRecord"]["Hash"].Value<string>());
                Assert.NotNull(json["Record"]["PackageRecord"]["Version"]);
                Assert.Equal("1.0.0", json["Record"]["PackageRecord"]["Version"].Value<string>());
                Assert.NotNull(json["Record"]["RegistrationRecord"]);
                Assert.NotNull(json["Record"]["RegistrationRecord"]["Id"]);
                Assert.Equal("b", json["Record"]["RegistrationRecord"]["Id"].Value<string>());
                Assert.NotNull(json["Record"]["Action"]);
                Assert.Equal("Create", json["Record"]["Action"].Value<string>());
                Assert.NotNull(json["Actor"]);
                Assert.NotNull(json["Actor"]["MachineName"]);
                Assert.Equal(Environment.MachineName, json["Actor"]["MachineName"].Value<string>());
                Assert.NotNull(json["Actor"]["MachineIP"]);
                Assert.True(IsValidMachineIpValue(json["Actor"]["MachineIP"].Value<string>()));
                Assert.NotNull(json["Actor"]["UserName"]);
                Assert.Equal($@"{Environment.UserDomainName}\{Environment.UserName}", json["Actor"]["UserName"].Value<string>());
                Assert.NotNull(json["Actor"]["AuthenticationType"]);
                Assert.Equal("MachineUser", json["Actor"]["AuthenticationType"].Value<string>());
                Assert.NotNull(json["Actor"]["TimestampUtc"]);
                Assert.InRange(DateTime.Parse(json["Actor"]["TimestampUtc"].Value<string>(), CultureInfo.InvariantCulture), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
                Assert.NotNull(json["Actor"]["OnBehalfOf"]);
                Assert.NotNull(json["Actor"]["OnBehalfOf"]["MachineName"]);
                Assert.Equal("a", json["Actor"]["OnBehalfOf"]["MachineName"].Value<string>());
                Assert.NotNull(json["Actor"]["OnBehalfOf"]["MachineIP"]);
                Assert.Equal("b", json["Actor"]["OnBehalfOf"]["MachineIP"].Value<string>());
                Assert.NotNull(json["Actor"]["OnBehalfOf"]["UserName"]);
                Assert.Equal("c", json["Actor"]["OnBehalfOf"]["UserName"].Value<string>());
                Assert.NotNull(json["Actor"]["OnBehalfOf"]["AuthenticationType"]);
                Assert.Equal("d", json["Actor"]["OnBehalfOf"]["AuthenticationType"].Value<string>());
                Assert.NotNull(json["Actor"]["OnBehalfOf"]["TimestampUtc"]);
                Assert.Equal(DateTime.MinValue, DateTime.Parse(json["Actor"]["OnBehalfOf"]["TimestampUtc"].Value<string>(), CultureInfo.InvariantCulture));
                Assert.Equal(JTokenType.Null, json["Actor"]["OnBehalfOf"]["OnBehalfOf"].Type);
            }
        }

        private static Task<AuditActor> GetOnBehalfOf()
        {
            var actor = new AuditActor(
                machineName: "a",
                machineIP: "b",
                userName: "c",
                authenticationType: "d",
                credentialKey: "e",
                timeStampUtc: DateTime.MinValue);

            return Task.FromResult(actor);
        }

        private static bool IsValidMachineIpValue(string ipAddress)
        {
            if (ipAddress == null)
            {
                return true;
            }

            return IPAddress.TryParse(ipAddress, out _);
        }
    }
}