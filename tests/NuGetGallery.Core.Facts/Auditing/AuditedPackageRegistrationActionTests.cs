// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Auditing
{
    public class AuditedPackageRegistrationActionTests : EnumTests
    {
        [Fact]
        public void Definition_HasNotChanged()
        {
            var expectedNames = new[]
            {
                "AddOwner",
                "RemoveOwner",
                "MarkVerified",
                "MarkUnverified",
                "SetRequiredSigner",
                "AddOwnershipRequest",
                "DeleteOwnershipRequest",
            };

            Verify(typeof(AuditedPackageRegistrationAction), expectedNames);
        }
    }
}