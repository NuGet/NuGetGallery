// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Auditing
{
    public class AuditedAuthenticatedOperationActionTests : EnumTests
    {
        [Fact]
        public void Definition_HasNotChanged()
        {
            var expectedNames = new[]
            {
                "FailedLoginInvalidPassword",
                "FailedLoginNoSuchUser",
                "FailedLoginUserIsOrganization",
                "PackagePushAttemptByNonOwner",
                "SymbolsPackagePushAttemptByNonOwner"
            };

            Verify(typeof(AuditedAuthenticatedOperationAction), expectedNames);
        }
    }
}