// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGetGallery.Auditing;

namespace NuGetGallery.Framework
{
    public class TestAuditingService : IAuditingService
    {
        private List<AuditRecord> _records = new List<AuditRecord>();

        public IReadOnlyList<AuditRecord> Records { get { return _records.AsReadOnly(); } }

        public Task SaveAuditRecordAsync(AuditRecord record)
        {
            _records.Add(record);
            return Task.FromResult(0);
        }
    }

    public static class AuditingServiceTestExtensions
    {
        public static bool WroteRecord<T>(this IAuditingService self) where T : AuditRecord
        {
            return self.WroteRecord<T>(ar => true);
        }

        public static bool WroteRecord<T>(this IAuditingService self, Func<T, bool> predicate) where T : AuditRecord
        {
            TestAuditingService testService = self as TestAuditingService;
            if (testService != null)
            {
                return testService.Records.OfType<T>().Any(predicate);
            }
            return false;
        }

        public static bool WroteRecordOfType<T>(this IAuditingService self) where T : AuditRecord
        {
            return WroteRecord<T>(self, _ => true);
        }
    }
}
