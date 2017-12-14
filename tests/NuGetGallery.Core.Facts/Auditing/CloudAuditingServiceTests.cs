// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class CloudAuditingServiceTests
    {
        [Fact]
        public void CloudAuditServiceObfuscateAuditRecord()
        {
            // Arrange
            CloudBlobContainer nullBlobContainer = null;
            var service = new CloudAuditingService("id", "1.1.1.1", nullBlobContainer, AuditActor.GetCurrentMachineActorAsync);

            AuditActor onBehalfOf = new AuditActor("machineName", "3.3.3.3", "userName1", "NoAuthentication", "someKey", DateTime.Now, null);
            AuditActor auditActor = new AuditActor("machineName", "2.2.2.2", "userName1", "NoAuthentication", "someKey", DateTime.Now, onBehalfOf);

            Package p = new Package()
            {
                User = new User("userName"),
                UserKey = 1,
                PackageRegistration = new PackageRegistration()
                {
                    Id = "regId"
                }
            };
            PackageAuditRecord packageAuditRecord = new PackageAuditRecord(p, AuditedPackageAction.Create);

            // Act 
            var auditEntry = service.RenderAuditEntry(new AuditEntry(packageAuditRecord, auditActor));

            // Assert
            var entry = (JObject)JsonConvert.DeserializeObject(auditEntry);

            var record = entry["Record"];
            var actor = entry["Actor"];

            Assert.Equal<string>("-1", record["PackageRecord"]["UserKey"].ToString());
            Assert.Equal<string>(string.Empty, record["PackageRecord"]["FlattenedAuthors"].ToString());
            Assert.Equal<string>("ObfuscatedUserName", actor["UserName"].ToString());
            Assert.Equal<string>("2.2.2.0", actor["MachineIP"].ToString());
            Assert.Equal<string>("ObfuscatedUserName", actor["OnBehalfOf"]["UserName"].ToString());
            Assert.Equal<string>("3.3.3.0", actor["OnBehalfOf"]["MachineIP"].ToString());
        }

        [Theory]
        [MemberData(nameof(OnlyPackageAuditRecordsWillBeSavedData))]
        public void OnlyPackageAuditRecordsWillBeSaved(AuditRecord record, bool expectedResult)
        {
            // Arrange
            CloudBlobContainer nullBlobContainer = null;
            var service = new CloudAuditingService("id", "1.1.1.1", nullBlobContainer, AuditActor.GetCurrentMachineActorAsync);

            // Act + Assert
            Assert.Equal<bool>(expectedResult, service.RecordWillBePersisted(record));
        }

        public static IEnumerable<object[]> OnlyPackageAuditRecordsWillBeSavedData
        {
            get
            {
                List<object[]> data = new List<object[]>();
                data.Add(new object[] { CreateUserAuditRecord(), false });
                data.Add(new object[] { CreateFailedAuthenticatedOperationAuditRecord(), false });
                data.Add(new object[] { CreateReservedNamespaceAuditRecord(), false });
                data.Add(new object[] { CreateUserSecurityPolicyAuditRecord(), false });
                data.Add(new object[] { CreatePackageRegistrationAuditRecord(), false });
                data.Add(new object[] { CreatePackageAuditRecord(), true });
                return data;
            }
        }

        static UserAuditRecord CreateUserAuditRecord()
        {
            return new UserAuditRecord(new User() { Username = "" }, AuditedUserAction.AddCredential);
        }

        static FailedAuthenticatedOperationAuditRecord CreateFailedAuthenticatedOperationAuditRecord()
        {
            return new FailedAuthenticatedOperationAuditRecord("", AuditedAuthenticatedOperationAction.FailedLoginNoSuchUser);
        }

        static ReservedNamespaceAuditRecord CreateReservedNamespaceAuditRecord()
        {
            return new ReservedNamespaceAuditRecord( new ReservedNamespace(), AuditedReservedNamespaceAction.AddOwner);
        }

        static UserSecurityPolicyAuditRecord CreateUserSecurityPolicyAuditRecord()
        {
            var policies = new List<UserSecurityPolicy>() { new UserSecurityPolicy() };
            return new UserSecurityPolicyAuditRecord("user", AuditedSecurityPolicyAction.Create, policies, true);
        }

        static PackageRegistrationAuditRecord CreatePackageRegistrationAuditRecord()
        {
            return new PackageRegistrationAuditRecord(new PackageRegistration(), AuditedPackageRegistrationAction.AddOwner, "");
        }

        static PackageAuditRecord CreatePackageAuditRecord()
        {
            return new PackageAuditRecord(new Package() { PackageRegistration = new PackageRegistration() { Id = "id" } }, AuditedPackageAction.Create);
        }
    }
}
