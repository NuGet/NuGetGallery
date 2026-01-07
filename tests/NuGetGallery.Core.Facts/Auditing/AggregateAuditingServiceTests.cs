// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class AggregateAuditingServiceTests
    {
        [Fact]
        public void Constructor_ThrowsForNull()
        {
            Assert.Throws<ArgumentNullException>(() => new AggregateAuditingService(services: null));
        }

        [Fact]
        public async Task SaveAuditRecordAsync_ThrowsForNull()
        {
            var services = Enumerable.Empty<IAuditingService>();
            var aggregatedService = new AggregateAuditingService(services);

            await Assert.ThrowsAsync<ArgumentNullException>(() => aggregatedService.SaveAuditRecordAsync(record: null));
        }

        [Fact]
        public async Task SaveAuditRecordAsync_AwaitsAllServices()
        {
            var services = CreateTestAuditingServices();
            var auditRecord = CreateAuditRecord();
            var aggregatedService = new AggregateAuditingService(services);

            await aggregatedService.SaveAuditRecordAsync(auditRecord);

            foreach (var service in services)
            {
                Assert.True(service.Awaited);
            }
        }

        private static AuditRecord CreateAuditRecord()
        {
            var packageRegistration = new PackageRegistration()
            {
                DownloadCount = 1,
                Id = "a",
                Key = 2
            };

            return new PackageRegistrationAuditRecord(packageRegistration, AuditedPackageRegistrationAction.AddOwner, owner: "b");
        }

        private static IEnumerable<TestAuditingService> CreateTestAuditingServices()
        {
            var services = new List<TestAuditingService>();

            for (var i = 0; i < 10; ++i)
            {
                services.Add(new TestAuditingService());
            }

            return services;
        }

        private class TestAuditingService : AuditingService
        {
            internal bool Awaited { get; private set; }

            protected override Task SaveAuditRecordAsync(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
            {
                Awaited = true;

                return Task.FromResult(0);
            }
        }
    }
}