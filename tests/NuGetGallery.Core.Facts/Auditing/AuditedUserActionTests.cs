﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGetGallery.Auditing
{
    public class AuditedUserActionTests : EnumTests
    {
        [Fact]
        public void Definition_HasNotChanged()
        {
            var expectedNames = new []
            {
                "AddCredential",
                "CancelChangeEmail",
                "ChangeEmail",
                "ConfirmEmail",
                "EditCredential",
                "ExpireCredential",
                "Login",
                "Register",
                "RemoveCredential",
                "RequestPasswordReset",
                "SubscribeToPolicies",
                "UnsubscribeFromPolicies",
                "AddOrganization",
                "TransformOrganization",
                "AddOrganizationMember",
                "RemoveOrganizationMember",
                "UpdateOrganizationMember"
            };

            Verify(typeof(AuditedUserAction), expectedNames);
        }
    }
}