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
            var service = new CloudAuditingServiceTest("", "", AuditActor.GetCurrentMachineActorAsync);

            // Act 
            var auditActorToBePersisted = await service.GetCloudAuditActorAsync();

            // Assert
            Assert.Equal<string>("ObfuscatedUserName", auditActorToBePersisted.UserName);
            Assert.Equal(true, IsObfuscatedIp(auditActorToBePersisted.MachineIP));
        }

        public bool IsObfuscatedIp(string ip)
        {
            IPAddress address;
            if (IPAddress.TryParse(ip, out address))
            {
                var bytes = address.GetAddressBytes();
                var length = bytes.Length;
                switch (length)
                {
                    case 4:
                         return bytes[3] == 0;
                    case 16:
                        for (int i = 8; i < 16; i++)
                        {
                            if( bytes[i] != 0 )
                            {
                                return false;
                            }
                        }
                        return true;
                    default:
                        break;
                }
            }
            return false;
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
                return GetRecordToPersist(record);
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
