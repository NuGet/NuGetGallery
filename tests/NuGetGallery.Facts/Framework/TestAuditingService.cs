// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Auditing;
using Xunit;

namespace NuGetGallery.Framework
{
    public class TestAuditingService : AuditingService
    {
        private List<AuditRecord> _records = new List<AuditRecord>();

        public IReadOnlyList<AuditRecord> Records { get { return _records.AsReadOnly(); } }

        public override Task<Uri> SaveAuditRecord(AuditRecord record)
        {
            _records.Add(record);
            return Task.FromResult(new Uri("http://nuget.local/auditing/test"));
        }

        protected override Task<Uri> SaveAuditRecord(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
        {
            // Not necessary since we override the only caller of this protected method
            throw new NotImplementedException();
        }
    }

    public static class AuditingServiceTestExtensions
    {
        public static bool WroteRecord<T>(this AuditingService self, Func<T, bool> predicate) where T : AuditRecord
        {
            TestAuditingService testService = self as TestAuditingService;
            if (testService != null)
            {
                return testService.Records.OfType<T>().Any(predicate);
            }
            return false;
        }

        public static bool WroteRecordOfType<T>(this AuditingService self) where T : AuditRecord
        {
            return WroteRecord<T>(self, _ => true);
        }
    }
}
