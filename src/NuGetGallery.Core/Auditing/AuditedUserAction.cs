// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Auditing
{
    public enum AuditedUserAction
    {
        Register,
        AddCredential,
        RemoveCredential,
        ExpireCredential,
        RevokeCredential,
        EditCredential,
        RequestPasswordReset,
        ChangeEmail,
        CancelChangeEmail,
        ConfirmEmail,
        Login,
        SubscribeToPolicies,
        UnsubscribeFromPolicies,
        AddOrganization,
        TransformOrganization,
        AddOrganizationMember,
        RemoveOrganizationMember,
        UpdateOrganizationMember,
        EnabledMultiFactorAuthentication,
        DisabledMultiFactorAuthentication,
        ExternalLoginAttempt,
    }
}