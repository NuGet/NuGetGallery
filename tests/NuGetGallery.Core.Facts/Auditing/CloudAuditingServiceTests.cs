// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
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
            var service = new CloudAuditingService(nullBlobContainer, AuditActor.GetCurrentMachineActorAsync);

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

            Assert.Equal("-1", record["PackageRecord"]["UserKey"].ToString());
            Assert.Equal(string.Empty, record["PackageRecord"]["FlattenedAuthors"].ToString());
            Assert.Equal("ObfuscatedUserName", actor["UserName"].ToString());
            Assert.Equal("2.2.2.0", actor["MachineIP"].ToString());
            Assert.Equal("ObfuscatedUserName", actor["OnBehalfOf"]["UserName"].ToString());
            Assert.Equal("3.3.3.0", actor["OnBehalfOf"]["MachineIP"].ToString());
        }

        [Theory]
        [MemberData(nameof(OnlyPackageAuditRecordsWillBeSavedData))]
        public void OnlyPackageAuditRecordsWillBeSaved(AuditRecord record, bool expectedResult)
        {
            // Arrange
            CloudBlobContainer nullBlobContainer = null;
            var service = new CloudAuditingService(nullBlobContainer, AuditActor.GetCurrentMachineActorAsync);

            // Act + Assert
            Assert.Equal<bool>(expectedResult, service.RecordWillBePersisted(record));
        }

        public static IEnumerable<object[]> OnlyPackageAuditRecordsWillBeSavedData
        {
            get
            {
                var data = new List<object[]>();

                data.Add(new object[] { CreateUserAuditRecord(), false });
                data.Add(new object[] { CreateFailedAuthenticatedOperationAuditRecord(), false });
                data.Add(new object[] { CreateReservedNamespaceAuditRecord(), false });
                data.Add(new object[] { CreateUserSecurityPolicyAuditRecord(), false });
                data.Add(new object[] { CreatePackageRegistrationAuditRecord(), false });
                data.Add(new object[] { CreatePackageAuditRecord(AuditedPackageAction.Create), false });
                data.Add(new object[] { CreatePackageAuditRecord(AuditedPackageAction.Delete), true });
                data.Add(new object[] { CreatePackageAuditRecord(AuditedPackageAction.Edit), false });
                data.Add(new object[] { CreatePackageAuditRecord(AuditedPackageAction.List), false });
                data.Add(new object[] { CreatePackageAuditRecord(AuditedPackageAction.SoftDelete), true });
#pragma warning disable CS0618
                data.Add(new object[] { CreatePackageAuditRecord(AuditedPackageAction.UndoEdit), false });
#pragma warning restore
                data.Add(new object[] { CreatePackageAuditRecord(AuditedPackageAction.Unlist), false });
                data.Add(new object[] { CreatePackageAuditRecord(AuditedPackageAction.Verify), false });
                data.Add(new object[] { CreatePackageAuditRecord(AuditedPackageAction.Deprecate), false });
                data.Add(new object[] { CreatePackageAuditRecord(AuditedPackageAction.Undeprecate), false });

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

        static PackageAuditRecord CreatePackageAuditRecord(AuditedPackageAction packageAction)
        {
            return new PackageAuditRecord(new Package() { PackageRegistration = new PackageRegistration() { Id = "id" } }, packageAction);
        }
    }
}