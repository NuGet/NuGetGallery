// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class PackageUndoSoftDeleteAuditRecordFacts
    {
        [Fact]
        public async Task PersistsUndoSoftDeleteThroughRealCloudSinkPath()
        {
            MemoryStream stream = null;
            var blob = new Mock<ISimpleCloudBlob>(MockBehavior.Strict);
            blob.Setup(b => b.OpenWriteAsync(It.IsAny<IAccessCondition>(), null))
                .ReturnsAsync(() => stream = new MemoryStream());
            var container = new Mock<ICloudBlobContainer>(MockBehavior.Strict);
            string savedPath = null;
            container.Setup(c => c.GetBlobReference(It.IsAny<string>()))
                .Callback<string>(path => savedPath = path).Returns(blob.Object);
            var actor = new AuditActor("machine", "1.2.3.4", "admin", "Admin", null, DateTime.UtcNow, null);
            var service = new CloudAuditingService(() => container.Object, () => Task.FromResult(actor));
            var record = new PackageUndoSoftDeleteAuditRecord(CreatePackage(), 42, "mistaken deletion", success: true, callerIdentity: "admin-api");

            await service.SaveAuditRecordAsync(record);

            Assert.StartsWith("package/example.package/1.0.0/", savedPath);
            Assert.EndsWith("-undosoftdelete.audit.v1.json", savedPath);
            var entry = JObject.Parse(Encoding.UTF8.GetString(stream.ToArray()));
            Assert.Equal("UndoSoftDelete", entry["Record"]["Action"].Value<string>());
            Assert.Equal(42, entry["Record"]["PackageDeleteKey"].Value<int>());
            Assert.Equal(20, entry["Record"]["PackageKey"].Value<int>());
            Assert.Equal("mistaken deletion", entry["Record"]["Reason"].Value<string>());
            Assert.True(entry["Record"]["Success"].Value<bool>());
            Assert.Null(entry["Record"]["Outcome"]);
            Assert.Null(entry["Record"]["Signature"]);
            Assert.Equal("admin-api", entry["Record"]["CallerIdentity"].Value<string>());
            Assert.NotNull(entry["Actor"]["TimestampUtc"]);
            Assert.NotNull(entry["Actor"]["OnBehalfOf"]);
            var failureRecord = new PackageUndoSoftDeleteAuditRecord(CreatePackage(), 42, "mistaken deletion", success: false, callerIdentity: "admin-api");
            await service.SaveAuditRecordAsync(failureRecord);

            entry = JObject.Parse(Encoding.UTF8.GetString(stream.ToArray()));
            Assert.False(entry["Record"]["Success"].Value<bool>());
            Assert.Null(entry["Record"]["Signature"]);
            Assert.Equal("admin-api", entry["Record"]["CallerIdentity"].Value<string>());
            blob.Verify(b => b.OpenWriteAsync(It.IsAny<IAccessCondition>(), null), Times.Exactly(2));
        }

        private static Package CreatePackage()
        {
            return new Package
            {
                Key = 20, Version = "1.0", NormalizedVersion = "1.0.0",
                PackageRegistration = new PackageRegistration { Id = "Example.Package" },
            };
        }
    }
}