// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using Xunit;

namespace NuGetGallery.Auditing
{
    public class AuditEntryTests
    {
        [Fact]
        public void Constructor_AcceptsNulls()
        {
            var entry = new AuditEntry(record: null, actor: null);

            Assert.Null(entry.Record);
            Assert.Null(entry.Actor);
        }

        [Fact]
        public void Constructor_SetsProperties()
        {
            var record = new Mock<AuditRecord>();
            var actor = new AuditActor(
                machineName: null,
                machineIP: null,
                userName: null,
                authenticationType: null,
                credentialKey: null,
                timeStampUtc: DateTime.MinValue);
            var entry = new AuditEntry(record.Object, actor);

            Assert.Same(record.Object, entry.Record);
            Assert.Same(actor, entry.Actor);
        }
    }
}