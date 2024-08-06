// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Auditing
{
    public class ScopeAuditRecordTests
    {
        [Theory]
        [InlineData(null, null, null)]
        [InlineData("", "", "")]
        [InlineData("a", "b", "c")]
        public void Constructor_SetsProperties(string owner, string subject, string allowedAction)
        {
            var entry = new ScopeAuditRecord(owner, subject, allowedAction);

            Assert.Equal(owner, entry.OwnerUsername);
            Assert.Equal(subject, entry.Subject);
            Assert.Equal(allowedAction, entry.AllowedAction);
        }
    }
}