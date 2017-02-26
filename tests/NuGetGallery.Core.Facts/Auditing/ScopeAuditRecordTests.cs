// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Auditing
{
    public class ScopeAuditRecordTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("a", "b")]
        public void Constructor_SetsProperties(string subject, string allowedAction)
        {
            var entry = new ScopeAuditRecord(subject, allowedAction);

            Assert.Equal(subject, entry.Subject);
            Assert.Equal(allowedAction, entry.AllowedAction);
        }
    }
}