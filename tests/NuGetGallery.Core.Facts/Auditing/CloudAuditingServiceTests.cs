// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
    }
}
