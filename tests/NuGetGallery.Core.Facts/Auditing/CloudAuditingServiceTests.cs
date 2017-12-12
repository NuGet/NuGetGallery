// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class CloudAuditingServiceTests
    {
        [Fact]
        public void CloudAuditServiceObfuscateAuditRecord()
        {
            // Arrange
            var service = new CloudAuditingServiceTest("", "", AuditActor.GetCurrentMachineActorAsync);
            var auditRecord = new CloudAuditRecordTest("action", "path");

            // Act 
            var auditRecordToBePersisted = service.GetCloudAuditRecord(auditRecord);

            // Assert
            Assert.Equal<string>("action_obfuscated", auditRecordToBePersisted.GetAction());
            Assert.Equal<string>("path_obfuscated", auditRecordToBePersisted.GetPath());
        }

        [Fact]
        public async Task CloudAuditServiceObfuscateAuditActor()
        {
            // Arrange
            var actor = await AuditActor.GetCurrentMachineActorAsync();
            var service = new CloudAuditingServiceTest("", "", () => Task.FromResult(actor));

            // Act 
            var auditActorToBePersisted = await service.GetCloudAuditActorAsync();

            // Assert
            Assert.Equal<string>("ObfuscatedUserName", auditActorToBePersisted.UserName);
            // The ObfuscateIp method is unit-tested individually.
            Assert.Equal(Obfuscator.ObfuscateIp(actor.MachineIP), auditActorToBePersisted.MachineIP);
        }

        public class CloudAuditingServiceTest : CloudAuditingService
        {
            public CloudAuditingServiceTest(string instanceId, string localIP, Func<Task<AuditActor>> getOnBehalfOf) : base (instanceId, localIP, getOnBehalfOf)
            {
            }

            public async Task<AuditActor> GetCloudAuditActorAsync()
            {
                return await GetActorAsync();
            }

            public AuditRecord GetCloudAuditRecord(AuditRecord record)
            {
                return PrepareTheRecordForPersistence(record);
            }
        }

        public class CloudAuditRecordTest : AuditRecord
        {
            string _action;
            string _path;

            public CloudAuditRecordTest(string action, string path)
            {
                _action = action;
                _path = path;
            }
            public override string GetAction()
            {
                return _action;
            }

            public override string GetPath()
            {
                return _path;
            }

            public override AuditRecord Obfuscate()
            {
                return new CloudAuditRecordTest($"{_action}_obfuscated", $"{_path}_obfuscated");
            }
        }
    }
}
